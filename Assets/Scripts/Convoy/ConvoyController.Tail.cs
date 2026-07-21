using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed partial class ConvoyController
    {
        private void UpdateTailCollision(float deltaTime) // 자기 꼬리 충돌
        {
            if (!EnableTailCollision)
            {
                return; // 기능 꺼짐
            }

            if (tailCutCooldownRemaining > 0f)
            {
                tailCutCooldownRemaining -= deltaTime; // 쿨타임 감소
                return; // 대기
            }

            int firstCollidableIndex = Mathf.Clamp(TailCollisionSafeSegmentCount, 0, segments.Count); // 앞쪽 제외
            firstCollidableIndex = Mathf.Max(firstCollidableIndex, GetFirstDetachableSegmentIndex()); // 스타터는 절단 보호
            float radiusSqr = TailCollisionRadius * TailCollisionRadius; // 제곱 반경
            Vector3 headPosition = transform.position; // 머리 위치

            if (TryCutAttachedTail(firstCollidableIndex, radiusSqr, headPosition))
            {
                return; // 붙은 꼬리 절단
            }
        }

        private bool TryCutAttachedTail(int firstIndex, float radiusSqr, Vector3 headPosition) // 붙은 꼬리 검사
        {
            for (int i = firstIndex; i < segments.Count; i++)
            {
                Transform segment = segments[i]; // 검사 몸통
                if (segment == null)
                {
                    continue; // 삭제됨
                }

                Vector3 offset = segment.position - headPosition; // 머리 거리
                offset.y = 0f; // 평면 판정
                if (offset.sqrMagnitude <= radiusSqr)
                {
                    CutTailFromIndex(i, headPosition); // 충돌 지점 절단
                    return true; // 처리됨
                }
            }

            return false; // 충돌 없음
        }

        private void CutTailFromIndex(int index, Vector3 burstCenter) // 꼬리 절단
        {
            index = Mathf.Max(index, GetFirstDetachableSegmentIndex()); // 스타터는 분리하지 않음
            if (index < 0 || index >= segments.Count)
            {
                return; // 범위 밖
            }

            Transform cutHead = segments[index]; // 분리 머리
            DetachedTailGroup group = CreateDetachedTailGroup(cutHead.position, cutHead.rotation); // 분리 그룹
            int cutCount = segments.Count - index; // 절단 수

            for (int i = index; i < segments.Count; i++)
            {
                Transform segment = segments[i]; // 분리 대상
                if (segment == null)
                {
                    continue; // 삭제됨
                }

                segment.SetParent(group.Root, true); // 월드 유지
                PrepareDetachedSegmentCollider(segment); // 충돌체 활성화
                group.Segments.Add(segment); // 그룹 등록
            }

            segments.RemoveRange(index, cutCount); // 체인 절단
            RemoveSegmentGroundChecks(index, cutCount); // 체크 절단
            RemoveSegmentRuntimes(index, cutCount); // 런타임 절단
            RegisterDetachedTailGroup(group); // 필드 잔존
            ApplyTailBurst(group, burstCenter); // 폭발 밀림
            tailCutCooldownRemaining = TailCutCooldown; // 연속 절단 방지
            NotifySegmentCountChanged(); // 길이 변경 알림
        }

        private DetachedTailGroup CreateDetachedTailGroup(Vector3 position, Quaternion rotation) // 분리 그룹 생성
        {
            GameObject root = new GameObject($"DetachedTailGroup_{++detachedTailSerial:00}"); // 그룹 루트
            root.transform.SetParent(DetachedTailRoot, false); // 분리 부모
            root.transform.SetPositionAndRotation(position, rotation); // 분리 머리 위치
            return new DetachedTailGroup(root.transform); // 그룹 데이터
        }

        private void RegisterDetachedTailGroup(DetachedTailGroup group) // 그룹 등록
        {
            if (group.Segments.Count == 0)
            {
                DestroyUnityObject(group.Root.gameObject); // 빈 그룹 제거
                return; // 등록 취소
            }

            RenameDetachedTailSegments(group); // 이름 정리
            LinkDetachedTailGroup(group); // 링크 구성
            group.Age = 0f; // 분리 시간 초기화
            group.SettledTime = 0f; // 안착 초기화
            group.RejoinReady = false; // 재결합 대기
            detachedTails.Add(group); // 목록 등록
        }

        private void PrepareDetachedSegmentCollider(Transform segment) // 분리 물리 준비
        {
            if (!EnableDetachedTailPhysics || segment == null)
            {
                return; // 사용 안 함
            }

            ClearDetachedSegmentJoints(segment); // 기존 링크 제거

            BoxCollider collider = segment.GetComponent<BoxCollider>(); // 분리 콜라이더
            if (collider == null)
            {
                collider = segment.gameObject.AddComponent<BoxCollider>(); // 충돌체 추가
            }

            collider.enabled = true; // 충돌 켬
            collider.isTrigger = false; // 밀림 사용
            collider.center = Vector3.zero; // 중심 정렬
            collider.size = Vector3.one; // 스케일 기준

            Rigidbody rigidbody = segment.GetComponent<Rigidbody>(); // 분리 바디
            if (rigidbody == null)
            {
                rigidbody = segment.gameObject.AddComponent<Rigidbody>(); // 바디 추가
            }

            rigidbody.isKinematic = false; // 개별 이동
            rigidbody.useGravity = true; // 바닥 낙하
            rigidbody.mass = DetachedTailMass; // 질량 적용
            rigidbody.linearDamping = DetachedTailLinearDamping; // 이동 감쇠
            rigidbody.angularDamping = DetachedTailAngularDamping; // 회전 감쇠
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate; // 보간
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 관통 완화
            rigidbody.constraints = RigidbodyConstraints.None; // 자유 물리
        }

        private void LinkDetachedTailGroup(DetachedTailGroup group) // 분리 링크 구성
        {
            if (!EnableDetachedTailPhysics || group == null)
            {
                return; // 사용 안 함
            }

            for (int i = 0; i < group.Segments.Count; i++)
            {
                Transform segment = group.Segments[i]; // 링크 대상
                if (segment == null)
                {
                    continue; // 삭제됨
                }

                ClearDetachedSegmentJoints(segment); // 링크 초기화

                if (i == 0)
                {
                    continue; // 분리 머리
                }

                Transform previous = group.Segments[i - 1]; // 앞 세그먼트
                if (previous == null)
                {
                    continue; // 연결 대상 없음
                }

                Rigidbody previousBody = previous.GetComponent<Rigidbody>(); // 앞 바디
                Rigidbody currentBody = segment.GetComponent<Rigidbody>(); // 현재 바디
                if (previousBody == null || currentBody == null)
                {
                    continue; // 물리 없음
                }

                ConfigurableJoint joint = segment.gameObject.AddComponent<ConfigurableJoint>(); // 링크 조인트
                joint.connectedBody = previousBody; // 앞 조각 연결
                joint.autoConfigureConnectedAnchor = false; // 수동 앵커
                joint.anchor = GetSegmentSocketLocalPointOrFallback(segment, FrontSocketName, Vector3.forward * (SegmentSpacing * 0.5f));
                joint.connectedAnchor = GetSegmentSocketLocalPointOrFallback(previous, RearSocketName, Vector3.back * (SegmentSpacing * 0.5f));
                joint.xMotion = ConfigurableJointMotion.Locked; // 거리 고정
                joint.yMotion = ConfigurableJointMotion.Locked; // 거리 고정
                joint.zMotion = ConfigurableJointMotion.Locked; // 거리 고정
                joint.angularXMotion = ConfigurableJointMotion.Limited; // 굽힘 제한
                joint.angularYMotion = ConfigurableJointMotion.Limited; // 굽힘 제한
                joint.angularZMotion = ConfigurableJointMotion.Limited; // 비틀림 제한
                joint.lowAngularXLimit = CreateJointLimit(-DetachedTailJointAngle); // X 하한
                joint.highAngularXLimit = CreateJointLimit(DetachedTailJointAngle); // X 상한
                joint.angularYLimit = CreateJointLimit(DetachedTailJointAngle); // Y 제한
                joint.angularZLimit = CreateJointLimit(DetachedTailJointAngle); // Z 제한
                joint.projectionMode = JointProjectionMode.PositionAndRotation; // 늘어짐 보정
                joint.projectionDistance = DetachedTailJointProjection; // 위치 보정
                joint.projectionAngle = DetachedTailJointAngle; // 각도 보정
                joint.enableCollision = false; // 인접 충돌 제외
                joint.breakForce = Mathf.Infinity; // 링크 유지
                joint.breakTorque = Mathf.Infinity; // 링크 유지
            }
        }

        private static void ClearDetachedSegmentJoints(Transform segment) // 링크 제거
        {
            if (segment == null)
            {
                return; // 대상 없음
            }

            ConfigurableJoint[] joints = segment.GetComponents<ConfigurableJoint>(); // 기존 링크
            for (int i = 0; i < joints.Length; i++)
            {
                DestroyUnityObject(joints[i]); // 링크 제거
            }
        }

        private static SoftJointLimit CreateJointLimit(float limit) // 조인트 제한값
        {
            SoftJointLimit jointLimit = default; // 제한값
            jointLimit.limit = limit; // 각도 설정
            return jointLimit; // 결과
        }

        private void ApplyTailBurst(DetachedTailGroup group, Vector3 burstCenter) // 절단 폭발 힘
        {
            if (!EnableDetachedTailPhysics || group == null)
            {
                return; // 사용 안 함
            }

            for (int i = 0; i < group.Segments.Count; i++)
            {
                Transform segment = group.Segments[i]; // 힘 대상
                if (segment == null)
                {
                    continue; // 삭제됨
                }

                Rigidbody rigidbody = segment.GetComponent<Rigidbody>(); // 분리 바디
                if (rigidbody == null)
                {
                    continue; // 물리 없음
                }

                Vector3 liftCenter = burstCenter; // 폭발 기준
                liftCenter.y = segment.position.y; // 평면 기준
                float headBias = i == 0 ? 1.8f : 1f; // 분리 머리 강조
                rigidbody.WakeUp(); // 즉시 반응
                rigidbody.AddExplosionForce(TailBurstForce * headBias, liftCenter, TailBurstRadius, TailBurstUpward, ForceMode.Impulse); // 바깥 밀림
                rigidbody.AddTorque(Random.insideUnitSphere * TailBurstTorque, ForceMode.Impulse); // 회전 밀림
            }
        }

        private void UpdateDetachedTailGroups(float deltaTime) // 분리 꼬리 갱신
        {
            CleanupDetachedTailGroups(); // 빈 그룹 정리

            for (int i = detachedTails.Count - 1; i >= 0; i--)
            {
                DetachedTailGroup group = detachedTails[i]; // 갱신 대상
                if (UpdateDetachedTailGroup(group, deltaTime))
                {
                    detachedTails.RemoveAt(i); // 재결합 완료
                }
            }
        }

        private bool UpdateDetachedTailGroup(DetachedTailGroup group, float deltaTime) // 단일 분리 갱신
        {
            if (!EnableDetachedTailRejoin || group == null)
            {
                HideRejoinArea(group); // 영역 숨김
                return false; // 유지
            }

            group.Age += deltaTime; // 분리 시간
            if (!IsDetachedTailSettled(group))
            {
                group.SettledTime = 0f; // 안착 해제
                group.RejoinReady = false; // 준비 취소
                HideRejoinArea(group); // 영역 숨김
                return false; // 유지
            }

            group.SettledTime += deltaTime; // 안착 누적
            if (group.SettledTime < DetachedTailSettleTime || group.Age < DetachedTailMinRejoinAge)
            {
                HideRejoinArea(group); // 대기 중
                return false; // 유지
            }

            group.RejoinReady = true; // 재결합 가능
            UpdateRejoinArea(group); // 원형 영역

            if (!IsPlayerTailEndInsideRejoinArea(group))
            {
                return false; // 아직 밖
            }

            return ReattachDetachedTail(group); // 재결합
        }

        private bool IsDetachedTailSettled(DetachedTailGroup group) // 안착 판정
        {
            bool hasBody = false; // 물리 존재
            float speedSqr = DetachedTailSettleSpeed * DetachedTailSettleSpeed; // 이동 기준
            float angularSqr = DetachedTailSettleAngularSpeed * DetachedTailSettleAngularSpeed; // 회전 기준

            for (int i = 0; i < group.Segments.Count; i++)
            {
                Transform segment = group.Segments[i]; // 검사 세그먼트
                if (segment == null)
                {
                    continue; // 삭제됨
                }

                Rigidbody rigidbody = segment.GetComponent<Rigidbody>(); // 분리 바디
                if (rigidbody == null)
                {
                    continue; // 물리 없음
                }

                hasBody = true; // 물리 확인
                if (rigidbody.linearVelocity.sqrMagnitude > speedSqr)
                {
                    return false; // 아직 이동 중
                }

                if (rigidbody.angularVelocity.sqrMagnitude > angularSqr)
                {
                    return false; // 아직 회전 중
                }
            }

            return hasBody; // 바디가 있어야 안착
        }

        private void UpdateRejoinArea(DetachedTailGroup group) // 재결합 영역 갱신
        {
            Transform head = GetDetachedTailHead(group); // 분리 머리
            if (head == null)
            {
                HideRejoinArea(group); // 머리 없음
                return; // 종료
            }

            EnsureRejoinArea(group); // 영역 보장
            group.RejoinCenter = GetRejoinAreaCenter(head); // 중심 갱신
            group.RejoinArea.position = group.RejoinCenter; // 위치 적용
            group.RejoinArea.rotation = Quaternion.identity; // 수평 유지
            group.RejoinArea.gameObject.SetActive(true); // 표시 켬
            RefreshRejoinAreaLine(group); // 원 갱신
        }

        private void EnsureRejoinArea(DetachedTailGroup group) // 재결합 영역 보장
        {
            if (group.RejoinArea != null)
            {
                return; // 기존 사용
            }

            GameObject area = new GameObject("RejoinArea"); // 영역 오브젝트
            area.transform.SetParent(group.Root, true); // 그룹 자식
            LineRenderer line = area.AddComponent<LineRenderer>(); // 원 라인
            line.loop = true; // 닫힌 원
            line.useWorldSpace = false; // 로컬 원
            line.widthMultiplier = 0.07f; // 선 두께
            line.sharedMaterial = GetRejoinAreaMaterial(); // 표시 재질
            group.RejoinArea = area.transform; // 영역 참조
            group.RejoinLine = line; // 라인 참조
        }

        private void RefreshRejoinAreaLine(DetachedTailGroup group) // 원 라인 갱신
        {
            if (group.RejoinLine == null)
            {
                return; // 라인 없음
            }

            int count = Mathf.Max(12, RejoinAreaSegments); // 분할 수
            float radius = GetEffectiveRejoinAreaRadius(); // 성장 반경
            group.RejoinLine.positionCount = count; // 점 개수
            group.RejoinLine.sharedMaterial = GetRejoinAreaMaterial(); // 재질 보정

            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f * i) / count; // 각도
                Vector3 point = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius); // 원점
                group.RejoinLine.SetPosition(i, point); // 점 적용
            }
        }

        private Material GetRejoinAreaMaterial() // 재결합 재질
        {
            if (rejoinAreaMaterial != null)
            {
                return rejoinAreaMaterial; // 캐시 사용
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit"); // URP 우선
            if (shader == null)
                shader = Shader.Find("Sprites/Default"); // fallback
            if (shader == null)
                shader = Shader.Find("Standard"); // 최종 fallback

            rejoinAreaMaterial = new Material(shader); // 런타임 재질
            SetMaterialColor(rejoinAreaMaterial, RejoinAreaColor); // 색 적용
            return rejoinAreaMaterial; // 결과
        }

        private static void SetMaterialColor(Material material, Color color) // 재질 색상
        {
            if (material == null)
            {
                return; // 재질 없음
            }

            material.color = color; // 기본 색
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color); // URP 색
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color); // 표준 색
        }

        private Vector3 GetRejoinAreaCenter(Transform head) // 재결합 중심
        {
            Vector3 forward = head.forward; // 머리 앞
            forward.y = 0f; // 평면화
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward; // fallback
            }

            Vector3 center = head.position + forward.normalized * RejoinAreaForwardOffset; // 앞쪽 위치
            center = GroundService.ProjectToGround(center, RejoinAreaHeight); // 바닥 높이
            return center; // 중심 반환
        }

        private bool IsPlayerTailEndInsideRejoinArea(DetachedTailGroup group) // 꼬리끝 겹침
        {
            Transform tailEnd = GetAttachedTailEnd(); // 플레이어 꼬리끝
            if (tailEnd == null || !group.RejoinReady)
            {
                return false; // 검사 불가
            }

            Vector3 offset = tailEnd.position - group.RejoinCenter; // 영역 거리
            offset.y = 0f; // 평면 판정
            float radius = GetEffectiveRejoinAreaRadius(); // 성장 반경
            return offset.sqrMagnitude <= radius * radius; // 원 안
        }

        private Transform GetAttachedTailEnd() // 플레이어 꼬리끝
        {
            if (segments.Count == 0)
            {
                return null; // 꼬리 없음
            }

            return segments[segments.Count - 1]; // 마지막 세그먼트
        }

        private Transform GetDetachedTailHead(DetachedTailGroup group) // 분리 꼬리 머리
        {
            if (group == null || group.Segments.Count == 0)
            {
                return null; // 머리 없음
            }

            return group.Segments[0]; // 첫 세그먼트
        }

        private bool ReattachDetachedTail(DetachedTailGroup group) // 꼬리 재결합
        {
            if (group == null || group.Segments.Count == 0 || SegmentRoot == null)
            {
                return false; // 재결합 불가
            }

            HideRejoinArea(group); // 영역 숨김

            for (int i = 0; i < group.Segments.Count; i++)
            {
                Transform segment = group.Segments[i]; // 연결 대상
                if (segment == null)
                {
                    continue; // 삭제됨
                }

                ClearDetachedSegmentJoints(segment); // 링크 제거
                DestroyUnityObject(segment.GetComponent<Rigidbody>()); // 물리 제거
                DestroyUnityObject(segment.GetComponent<Collider>()); // 충돌 제거
                segment.SetParent(SegmentRoot, true); // 연결 루트
                segments.Add(segment); // 체인 복귀
                segmentGroundChecks.Add(GetSegmentGroundCheck(segment)); // 바닥 체크 복귀
                segmentRuntimes.Add(GetSegmentRuntime(segment, segments.Count - 1, true)); // 런타임 복귀
                segment.name = $"ConvoySegment_{segments.Count:00}"; // 이름 정리
                ApplySegmentMaterial(segment, segments.Count - 1); // 재질 정리
            }

            group.Segments.Clear(); // 분리 목록 비움
            if (group.Root != null)
            {
                DestroyUnityObject(group.Root.gameObject); // 빈 그룹 제거
            }

            NotifySegmentCountChanged(); // 길이 변경 알림
            return true; // 재결합 완료
        }

        private static void HideRejoinArea(DetachedTailGroup group) // 재결합 영역 숨김
        {
            if (group == null || group.RejoinArea == null)
            {
                return; // 영역 없음
            }

            group.RejoinArea.gameObject.SetActive(false); // 표시 끔
        }

        private void CleanupDetachedTailGroups() // 분리 꼬리 정리
        {
            for (int i = detachedTails.Count - 1; i >= 0; i--)
            {
                DetachedTailGroup group = detachedTails[i]; // 검사 그룹
                group.Segments.RemoveAll(segment => segment == null); // null 제거
                if (group.Segments.Count > 0)
                {
                    continue; // 유지
                }

                if (group.Root != null)
                {
                    DestroyUnityObject(group.Root.gameObject); // 빈 루트 제거
                }

                detachedTails.RemoveAt(i); // 목록 제거
            }
        }

        private static void RenameDetachedTailSegments(DetachedTailGroup group) // 분리 이름 정리
        {
            if (group == null)
            {
                return; // 대상 없음
            }

            for (int i = 0; i < group.Segments.Count; i++)
            {
                Transform segment = group.Segments[i]; // 이름 대상
                if (segment != null)
                {
                    segment.name = $"DetachedConvoySegment_{i + 1:00}"; // 분리 몸통명
                }
            }
        }
    }
}
