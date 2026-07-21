using UnityEngine;

namespace TeamProject01.Gameplay
{
    public enum BossDiamondProjectileMode
    {
        Straight = 0, // Normal ?곹깭?먯꽌 Nexus 以묒떖?쇰줈 吏곸꽑 ?대룞?섎뒗 紐⑤뱶
        FormationHoming = 1 // Berserk ?곹깭?먯꽌 ??? ?먰샇, ?뚯쭊 ?쒖꽌濡??대룞?섎뒗 紐⑤뱶
    }

    public sealed class BossDiamondProjectile : MonoBehaviour
    {
        private enum FormationHomingState
        {
            MovingToFormation = 0, // ?앹꽦 ?꾩튂?먯꽌 ????????꾩튂濡??대룞?섎뒗 ?곹깭
            Standby = 1, // ??뺤쓣 ?좎??섎ŉ ?먯떊??異쒓꺽 ?쒖꽌瑜?湲곕떎由щ뒗 ?곹깭
            ArcFlight = 2, // Nexus瑜?以묒떖?쇰줈 ?먰샇瑜?洹몃━硫??대룞?섎뒗 ?곹깭
            DiveAttack = 3 // ?먰샇 ?대룞???앸궡怨?Nexus濡?怨좎냽 ?뚯쭊?섎뒗 ?곹깭
        }

        [Header("Projectile")]
        [Min(0)]
        [SerializeField] private int nexusDamage = 1; // Nexus???꾩갑?덉쓣 ???곸슜???쇳빐??

        [Min(0.1f)]
        [SerializeField] private float moveSpeed = 6.0f; // Normal ?ъ궗泥댁쓽 吏곸꽑 ?대룞 ?띾룄

        [Min(0.1f)]
        [SerializeField] private float hitDistance = 0.7f; // Normal ?ъ궗泥댁쓽 Nexus ?꾩갑 ?먯젙 嫄곕━

        [Min(0.1f)]
        [SerializeField] private float lifeTime = 20.0f; // ?ъ궗泥닿? 議댁옱?????덈뒗 理쒕? ?쒓컙

        [Header("Formation")]
        [Min(0.0f)]
        [SerializeField] private float formationDuration = 0.6f; // ?앹꽦 ?꾩튂?먯꽌 ????꾩튂源뚯? ?대룞?섎뒗 ?쒓컙

        [Header("Berserk Arc Flight")]
        [Min(1.0f)]
        [SerializeField] private float arcAngularSpeed = 140.0f; // Nexus 二쇰????뚯쟾?섎뒗 珥덈떦 媛곷룄

        [Min(0.0f)]
        [SerializeField] private float minimumArcDuration = 0.6f; // ?뚯쭊 ??理쒖냼 ?먰샇 ?대룞 ?쒓컙

        [Min(0.0f)]
        [SerializeField] private float maximumArcDuration = 1.4f; // ?뚯쭊 ??理쒕? ?먰샇 ?대룞 ?쒓컙

        [Header("Berserk Dive Attack")]
        [Min(0.1f)]
        [SerializeField] private float diveSpeed = 18.0f; // ?먰샇 ?대룞 ??Nexus濡??뚯쭊?섎뒗 ?띾룄

        [Min(0.0f)]
        [SerializeField] private float diveImpactRadius = 0.8f; // Nexus 以묒떖?먯꽌 議곌툑 ?⑥뼱吏?異⑸룎 吏?먯쓽 諛섏?由?

        [Min(0.1f)]
        [SerializeField] private float diveHitDistance = 0.5f; // ?뚯쭊 紐⑺몴???꾩갑 ?먯젙 嫄곕━

        private Transform target; // ?ㅼ젣 ?쇳빐瑜?諛쏆쓣 Nexus Transform

        private BossDiamondProjectileMode projectileMode; // ?꾩옱 ?ъ궗泥댁쓽 ?대룞 諛⑹떇

        private FormationHomingState formationHomingState; // Berserk ?ъ궗泥댁쓽 ?꾩옱 ?대룞 ?④퀎

        private Vector3 formationStartPosition; // ????대룞???쒖옉???붾뱶 ?꾩튂

        private Vector3 formationTargetPosition; // ??뺤뿉???湲고븷 ?붾뱶 ?꾩튂

        private Vector3 homingTargetOffset; // Nexus 以묒떖??湲곗??쇰줈 ?꾨떖諛쏆? ?됰㈃ 諛⑺뼢媛?

        private Vector3 movementDirection; // ?꾩옱 ?ъ궗泥닿? ?대룞?섎뒗 諛⑺뼢

        private Vector3 diveTargetPosition; // ?먰샇 ?대룞 ??理쒖쥌?곸쑝濡??뚯쭊??Nexus 二쇰? ?꾩튂

        private float standbyDuration; // ??뺤뿉???먯떊??異쒓꺽 ?쒖꽌瑜?湲곕떎由??쒓컙

        private float lifeTimer; // ?ъ궗泥닿? ?앹꽦????吏???쒓컙

        private float modeTimer; // ?꾩옱 ?대룞 ?곹깭?먯꽌 吏???쒓컙

        private float arcCurrentAngle; // Nexus瑜?湲곗??쇰줈 ???꾩옱 ?먰샇 媛곷룄

        private float arcRadius; // Nexus瑜?湲곗??쇰줈 ???꾩옱 ?먰샇 諛섏?由?

        private float arcFlightDuration; // ?대쾲 ?ъ궗泥닿? ?먰샇 ?대룞???좎????쒓컙

        private float arcFlightHeight; // ?먰샇 ?대룞 以??좎???Y異??믪씠

        private float arcDirectionSign; // ?쒓퀎 ?먮뒗 諛섏떆怨?諛⑺뼢???섑??대뒗 媛?

        private bool isConfigured; // 紐⑺몴? ?대룞 諛⑹떇???ㅼ젙?먮뒗吏 ?섑??대뒗 媛?

        private bool isDestroyed; // ?쒓굅 泥섎━媛 ?쒖옉?먮뒗吏 ?섑??대뒗 媛?

        public bool IsDestroyed
        {
            get
            {
                return isDestroyed; // ?몃??먯꽌 ?ъ궗泥댁쓽 ?쒓굅 ?곹깭瑜??뺤씤?????덇쾶 諛섑솚?쒕떎.
            }
        }

        public void SetNexusDamage(int damage)
        {
            nexusDamage = Mathf.Max(0, damage);
        }

        private void Update()
        {
            if (isDestroyed)
            {
                return;
            }

            lifeTimer += Time.deltaTime; // ?ъ궗泥닿? 議댁옱???쒓컙??利앷??쒗궓??

            if (lifeTimer >= lifeTime) // 理쒕? ?좎? ?쒓컙???섏뿀?ㅻ㈃
            {
                DestroyProjectile(); // 紐⑺몴???꾩갑?섏? 紐삵뻽?붾씪???쒓굅?쒕떎.
                return;
            }

            if (!isConfigured) // ?꾩쭅 Nexus 紐⑺몴媛 ?ㅼ젙?섏? ?딆븯?ㅻ㈃
            {
                return; // ?대룞?섏? ?딅뒗??
            }

            if (target == null) // Nexus媛 ?쒓굅?먭굅??李몄“瑜??껋뿀?ㅻ㈃
            {
                DestroyProjectile(); // ?대룞??紐⑺몴媛 ?놁쑝誘濡??쒓굅?쒕떎.
                return;
            }

            if (projectileMode == BossDiamondProjectileMode.FormationHoming) // Berserk ?ъ궗泥대씪硫?
            {
                MoveFormationHoming(); // ??? ?먰샇, ?뚯쭊 ?대룞??泥섎━?쒕떎.
                return;
            }

            if (TryHitNormalTarget()) // Normal ?ъ궗泥닿? ?대? Nexus ?꾩갑 踰붿쐞 ?덉씠?쇰㈃
            {
                return; // ?쇳빐 泥섎━媛 ?앸궗?쇰?濡??대룞?섏? ?딅뒗??
            }

            MoveStraight(); // Normal ?ъ궗泥대? Nexus 以묒떖?쇰줈 ?대룞?쒗궓??

            TryHitNormalTarget(); // ?대룞 ??Nexus ?꾩갑 ?щ?瑜??ㅼ떆 ?뺤씤?쒕떎.
        }

        public void Configure(Transform target) // Normal 吏곸꽑 ?ъ궗泥대? ?ㅼ젙?섎뒗 ?⑥닔
        {
            InitializeProjectile(target); // 怨듯넻 ?ъ궗泥??곹깭瑜?珥덇린?뷀븳??

            projectileMode = BossDiamondProjectileMode.Straight; // 吏곸꽑 ?대룞 紐⑤뱶濡??ㅼ젙?쒕떎.

            if (target == null) // ?좏슚??Nexus媛 ?녿떎硫?
            {
                return; // ?대룞 諛⑺뼢??怨꾩궛?섏? ?딅뒗??
            }

            movementDirection = target.position - transform.position; // ?꾩옱 ?꾩튂?먯꽌 Nexus源뚯???諛⑺뼢??怨꾩궛?쒕떎.

            if (movementDirection.sqrMagnitude <= 0.0001f) // 諛⑺뼢??怨꾩궛?????녿떎硫?
            {
                return; // ?꾩옱 ?뚯쟾???좎??쒕떎.
            }

            movementDirection.Normalize(); // ?대룞 諛⑺뼢??湲몄씠瑜?1濡?留뚮뱺??
            transform.rotation = Quaternion.LookRotation(movementDirection, Vector3.up); // Nexus 諛⑺뼢??諛붾씪蹂닿쾶 ?쒕떎.
        }

        public void ConfigureFormationHoming(Transform target, Vector3 formationPosition, float standbyDuration) // 湲곗〈 3媛?留ㅺ컻蹂???곌껐???⑥닔
        {
            ConfigureFormationHoming(target, formationPosition, standbyDuration, Vector3.zero); // 蹂꾨룄 諛⑺뼢???놁쑝硫??먮룞?쇰줈 諛⑺뼢??怨꾩궛?쒕떎.
        }

        public void ConfigureFormationHoming(Transform target, Vector3 formationPosition, float standbyDuration, Vector3 homingTargetOffset) // Berserk ?ъ궗泥대? ?ㅼ젙?섎뒗 ?⑥닔
        {
            InitializeProjectile(target); // 怨듯넻 ?ъ궗泥??곹깭瑜?珥덇린?뷀븳??

            projectileMode = BossDiamondProjectileMode.FormationHoming; // Berserk ?대룞 紐⑤뱶濡??ㅼ젙?쒕떎.
            formationHomingState = FormationHomingState.MovingToFormation; // ????대룞 ?곹깭遺???쒖옉?쒕떎.

            formationStartPosition = transform.position; // ?꾩옱 ?앹꽦 ?꾩튂瑜?????대룞 ?쒖옉?먯쑝濡???ν븳??
            formationTargetPosition = formationPosition; // 怨듦꺽 Script媛 怨꾩궛??????꾩튂瑜???ν븳??
            this.standbyDuration = Mathf.Max(0.0f, standbyDuration); // ?湲곗떆媛꾩쓣 0 ?댁긽?쇰줈 ??ν븳??

            this.homingTargetOffset = homingTargetOffset; // Nexus 二쇰??먯꽌 ?ъ슜??異⑸룎 諛⑺뼢????ν븳??
            this.homingTargetOffset.y = 0.0f; // 異⑸룎 諛⑺뼢???곹븯 遺꾩궛???쒓굅?쒕떎.

            movementDirection = formationTargetPosition - formationStartPosition; // ?앹꽦?먯뿉??????꾩튂源뚯???諛⑺뼢??怨꾩궛?쒕떎.

            if (movementDirection.sqrMagnitude <= 0.0001f) // ????대룞 諛⑺뼢??怨꾩궛?????녿떎硫?
            {
                movementDirection = transform.forward; // ?꾩옱 諛붾씪蹂대뒗 諛⑺뼢??????ъ슜?쒕떎.
            }

            movementDirection.Normalize(); // ?대룞 諛⑺뼢??湲몄씠瑜?1濡?留뚮뱺??
            transform.rotation = Quaternion.LookRotation(movementDirection, Vector3.up); // ????꾩튂 諛⑺뼢??諛붾씪蹂닿쾶 ?쒕떎.
        }

        private void InitializeProjectile(Transform target) // 紐⑤뱺 諛쒖궗 諛⑹떇?먯꽌 ?ъ슜?섎뒗 怨듯넻 珥덇린???⑥닔
        {
            this.target = target; // ?꾨떖諛쏆? Nexus Transform????ν븳??

            lifeTimer = 0.0f; // ?좎??쒓컙??珥덇린?뷀븳??
            modeTimer = 0.0f; // ?곹깭 ??대㉧瑜?珥덇린?뷀븳??
            standbyDuration = 0.0f; // ?댁쟾 ?湲곗떆媛꾩쓣 珥덇린?뷀븳??
            homingTargetOffset = Vector3.zero; // ?댁쟾 紐⑺몴 諛⑺뼢??珥덇린?뷀븳??
            diveTargetPosition = Vector3.zero; // ?댁쟾 ?뚯쭊 紐⑺몴?먯쓣 珥덇린?뷀븳??
            isDestroyed = false; // ?쒓굅?섏? ?딆? ?곹깭濡?珥덇린?뷀븳??
            isConfigured = target != null; // ?좏슚??Nexus媛 ?덈떎硫??ㅼ젙 ?꾨즺 ?곹깭濡???ν븳??
        }

        private void MoveStraight() // Normal ?ъ궗泥대? Nexus 以묒떖?쇰줈 吏곸꽑 ?대룞?쒗궎???⑥닔
        {
            Vector3 offset = target.position - transform.position; // ?꾩옱 ?꾩튂?먯꽌 Nexus源뚯???諛⑺뼢??怨꾩궛?쒕떎.

            if (offset.sqrMagnitude <= 0.0001f) // ?대룞 諛⑺뼢??怨꾩궛?????녿떎硫?
            {
                return; // ?꾩옱 ?꾩튂瑜??좎??쒕떎.
            }

            movementDirection = offset.normalized; // Nexus 諛⑺뼢??湲몄씠瑜?1濡?留뚮뱺??

            transform.position = Vector3.MoveTowards(transform.position, target.position, moveSpeed * Time.deltaTime); // Nexus 以묒떖?쇰줈 吏곸꽑 ?대룞?쒕떎.
            transform.rotation = Quaternion.LookRotation(movementDirection, Vector3.up); // ?대룞 諛⑺뼢??諛붾씪蹂닿쾶 ?쒕떎.
        }

        private void MoveFormationHoming() // Berserk ?ъ궗泥댁쓽 ?꾩껜 ?대룞 ?④퀎瑜?泥섎━?섎뒗 ?⑥닔
        {
            if (formationHomingState == FormationHomingState.MovingToFormation) // ????꾩튂濡??대룞 以묒씠?쇰㈃
            {
                MoveToFormation(); // 吏?뺣맂 ????꾩튂濡??대룞?쒕떎.
                return;
            }

            if (formationHomingState == FormationHomingState.Standby) // ??뺤뿉???湲?以묒씠?쇰㈃
            {
                WaitInFormation(); // ????꾩튂瑜??좎??쒕떎.
                return;
            }

            if (formationHomingState == FormationHomingState.ArcFlight) // Nexus 二쇰????뚯쟾 以묒씠?쇰㈃
            {
                MoveArcFlight(); // ?먰샇 ?대룞??泥섎━?쒕떎.
                return;
            }

            MoveDiveAttack(); // ?먰샇 ?대룞 ??Nexus ?뚯쭊??泥섎━?쒕떎.
        }

        private void MoveToFormation() // ?앹꽦 ?꾩튂?먯꽌 ????꾩튂濡??쇱퀜吏???⑥닔
        {
            modeTimer += Time.deltaTime; // ????대룞 ?쒓컙??利앷??쒗궓??

            float progress = formationDuration <= 0.0f ? 1.0f : Mathf.Clamp01(modeTimer / formationDuration); // ?대룞 吏꾪뻾瑜좎쓣 怨꾩궛?쒕떎.
            float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, progress); // ?쒖옉怨??앹쓣 遺?쒕읇寃?留뚮뱺??

            Vector3 nextPosition = Vector3.Lerp(formationStartPosition, formationTargetPosition, easedProgress); // ?대쾲 ?꾨젅???꾩튂瑜?怨꾩궛?쒕떎.
            Vector3 nextDirection = nextPosition - transform.position; // ?ㅼ젣 ?대룞 諛⑺뼢??怨꾩궛?쒕떎.

            transform.position = nextPosition; // 怨꾩궛???꾩튂濡??대룞?쒕떎.

            if (nextDirection.sqrMagnitude > 0.0001f) // ?좏슚???대룞 諛⑺뼢???덈떎硫?
            {
                transform.rotation = Quaternion.LookRotation(nextDirection.normalized, Vector3.up); // ?대룞 諛⑺뼢??諛붾씪蹂닿쾶 ?쒕떎.
            }

            if (progress < 1.0f) // ?꾩쭅 ????꾩튂???꾩갑?섏? ?딆븯?ㅻ㈃
            {
                return; // ?湲??곹깭濡??섏뼱媛吏 ?딅뒗??
            }

            transform.position = formationTargetPosition; // 理쒖쥌 ????꾩튂瑜??뺥솗??留욎텣??
            formationHomingState = FormationHomingState.Standby; // ????湲??곹깭濡?蹂寃쏀븳??
            modeTimer = 0.0f; // ?湲곗떆媛?怨꾩궛???꾪빐 ??대㉧瑜?珥덇린?뷀븳??
        }

        private void WaitInFormation() // ??뺤쓣 ?좎??섎ŉ ?먯떊??異쒓꺽 ?쒓컙??湲곕떎由щ뒗 ?⑥닔
        {
            transform.position = formationTargetPosition; // ?湲?以묒뿉??吏?뺣맂 ????꾩튂瑜??좎??쒕떎.

            modeTimer += Time.deltaTime; // ?湲곗떆媛꾩쓣 利앷??쒗궓??

            if (modeTimer < standbyDuration) // ?꾩쭅 ?먯떊??異쒓꺽 ?쒓컙???섏? ?딆븯?ㅻ㈃
            {
                return; // ??뺤쓣 怨꾩냽 ?좎??쒕떎.
            }

            BeginArcFlight(); // Nexus 二쇰? ?먰샇 ?대룞???쒖옉?쒕떎.
        }

        private void BeginArcFlight() // ?먰샇 ?대룞???쒖옉?섍린 ?꾪븳 媛믪쓣 怨꾩궛?섎뒗 ?⑥닔
        {
            formationHomingState = FormationHomingState.ArcFlight; // ?먰샇 ?대룞 ?곹깭濡?蹂寃쏀븳??
            modeTimer = 0.0f; // ?먰샇 ?대룞 ?쒓컙??珥덇린?뷀븳??

            Vector3 startOffset = transform.position - target.position; // Nexus 以묒떖?먯꽌 ?ъ궗泥닿퉴吏??諛⑺뼢??怨꾩궛?쒕떎.
            startOffset.y = 0.0f; // ?먰샇 ?대룞??XZ ?됰㈃?쇰줈 ?쒗븳?쒕떎.

            if (startOffset.sqrMagnitude <= 0.0001f) // ?먰샇 ?쒖옉 諛⑺뼢??怨꾩궛?????녿떎硫?
            {
                startOffset = -transform.forward; // ?꾩옱 ??諛⑺뼢??諛섎?瑜??ъ슜?쒕떎.
                startOffset.y = 0.0f; // Y異뺤쓣 ?쒓굅?쒕떎.
            }

            arcRadius = Mathf.Max(0.1f, startOffset.magnitude); // ?꾩옱 Nexus???嫄곕━瑜??먰샇 諛섏?由꾩쑝濡???ν븳??
            arcCurrentAngle = Mathf.Atan2(startOffset.z, startOffset.x) * Mathf.Rad2Deg; // ?꾩옱 Nexus 湲곗? 媛곷룄瑜???ν븳??
            arcDirectionSign = Random.value < 0.5f ? -1.0f : 1.0f; // ?쒓퀎 ?먮뒗 諛섏떆怨?諛⑺뼢??臾댁옉?꾨줈 ?좏깮?쒕떎.

            float minimumDuration = Mathf.Min(minimumArcDuration, maximumArcDuration); // ???쒓컙 以??묒? 媛믪쓣 理쒖냼 ?쒓컙?쇰줈 ?ъ슜?쒕떎.
            float maximumDuration = Mathf.Max(minimumArcDuration, maximumArcDuration); // ???쒓컙 以???媛믪쓣 理쒕? ?쒓컙?쇰줈 ?ъ슜?쒕떎.

            arcFlightDuration = Random.Range(minimumDuration, maximumDuration); // 媛??ъ궗泥대쭏???쒕줈 ?ㅻⅨ ?먰샇 ?좎? ?쒓컙???좏깮?쒕떎.
            arcFlightHeight = transform.position.y; // ??뺤뿉??異쒓꺽???믪씠瑜??먰샇 ?대룞 以??좎??쒕떎.
        }

        private void MoveArcFlight() // Nexus瑜?以묒떖?쇰줈 ?먰샇瑜?洹몃━硫??대룞?섎뒗 ?⑥닔
        {
            modeTimer += Time.deltaTime; // ?먰샇 ?대룞 ?쒓컙??利앷??쒗궓??
            arcCurrentAngle += arcDirectionSign * arcAngularSpeed * Time.deltaTime; // ?쒓퀎 ?먮뒗 諛섏떆怨?諛⑺뼢?쇰줈 媛곷룄瑜?利앷??쒗궓??

            float angleRadians = arcCurrentAngle * Mathf.Deg2Rad; // ?먰샇 ?꾩튂 怨꾩궛???꾪빐 媛곷룄瑜??쇰뵒?덉쑝濡?蹂?섑븳??

            Vector3 planarOffset = new Vector3(Mathf.Cos(angleRadians), 0.0f, Mathf.Sin(angleRadians)) * arcRadius; // Nexus 湲곗? ?먰삎 ?꾩튂瑜?怨꾩궛?쒕떎.
            Vector3 nextPosition = target.position + planarOffset; // Nexus ?꾩튂???먰삎 ?ㅽ봽?뗭쓣 ?뷀빐 ?붾뱶 ?꾩튂瑜?怨꾩궛?쒕떎.

            nextPosition.y = arcFlightHeight; // ?먰샇 ?대룞 以묒뿉??Y異??믪씠瑜??좎??쒕떎.

            Vector3 nextDirection = nextPosition - transform.position; // ?먰샇???ㅼ젣 ?묒꽑 ?대룞 諛⑺뼢??怨꾩궛?쒕떎.

            transform.position = nextPosition; // 怨꾩궛???먰샇 ?꾩튂濡??대룞?쒕떎.

            if (nextDirection.sqrMagnitude > 0.0001f) // ?ㅼ젣 ?대룞 諛⑺뼢???덈떎硫?
            {
                nextDirection.y = 0.0f; // ?꾩븘?섎줈 湲곗슱?댁?吏 ?딅룄濡?Y異뺤쓣 ?쒓굅?쒕떎.

                if (nextDirection.sqrMagnitude > 0.0001f) // ?됰㈃ ?대룞 諛⑺뼢???좏슚?섎떎硫?
                {
                    transform.rotation = Quaternion.LookRotation(nextDirection.normalized, Vector3.up); // ?먰샇???묒꽑 諛⑺뼢??諛붾씪蹂닿쾶 ?쒕떎.
                }
            }

            if (modeTimer < arcFlightDuration) // ?꾩쭅 ?좏깮???먰샇 ?대룞 ?쒓컙??吏?섏? ?딆븯?ㅻ㈃
            {
                return; // 怨꾩냽 Nexus 二쇰????뚯쟾?쒕떎.
            }

            BeginDiveAttack(); // ?먰샇 ?대룞???앸궡怨?Nexus ?뚯쭊???쒖옉?쒕떎.
        }

        private void BeginDiveAttack() // Nexus 二쇰????쒕줈 ?ㅻⅨ 吏?먯쑝濡??뚯쭊???쒖옉?섎뒗 ?⑥닔
        {
            formationHomingState = FormationHomingState.DiveAttack; // ?뚯쭊 ?곹깭濡?蹂寃쏀븳??
            modeTimer = 0.0f; // ?뚯쭊 ?곹깭 ??대㉧瑜?珥덇린?뷀븳??
            GameplaySfxEmitter.TryPlayAt(transform, GameplaySfxCue.BossDiamondBurstSmall, transform.position, true);

            Vector3 impactDirection = homingTargetOffset; // 怨듦꺽 Script媛 ?꾨떖??醫뙿룹슦쨌?왖룸뮘쨌?媛곸꽑 諛⑺뼢??媛?몄삩??
            impactDirection.y = 0.0f; // 異⑸룎 諛⑺뼢?먯꽌 Y異뺤쓣 ?쒓굅?쒕떎.

            if (impactDirection.sqrMagnitude <= 0.0001f) // ?꾨떖諛쏆? 諛⑺뼢???녿떎硫?
            {
                impactDirection = transform.position - target.position; // ?꾩옱 ?먰샇 ?꾩튂 諛⑺뼢???ъ슜?쒕떎.
                impactDirection.y = 0.0f; // Y異뺤쓣 ?쒓굅?쒕떎.
            }

            if (impactDirection.sqrMagnitude <= 0.0001f) // ?꾩옱 ?꾩튂 諛⑺뼢??怨꾩궛?????녿떎硫?
            {
                impactDirection = Vector3.forward; // 湲곕낯 ??諛⑺뼢???ъ슜?쒕떎.
            }

            impactDirection.Normalize(); // 異⑸룎 諛⑺뼢??湲몄씠瑜?1濡?留뚮뱺??

            diveTargetPosition = target.position + impactDirection * diveImpactRadius; // Nexus 以묒떖 二쇰????쒕줈 ?ㅻⅨ 異⑸룎 吏?먯쓣 怨꾩궛?쒕떎.

            movementDirection = diveTargetPosition - transform.position; // ?꾩옱 ?먰샇 ?꾩튂?먯꽌 ?뚯쭊 紐⑺몴?먭퉴吏??諛⑺뼢??怨꾩궛?쒕떎.

            if (movementDirection.sqrMagnitude > 0.0001f) // ?뚯쭊 諛⑺뼢???좏슚?섎떎硫?
            {
                movementDirection.Normalize(); // ?뚯쭊 諛⑺뼢??湲몄씠瑜?1濡?留뚮뱺??
                transform.rotation = Quaternion.LookRotation(movementDirection, Vector3.up); // 利됱떆 ?뚯쭊 諛⑺뼢?쇰줈 湲됱꽑?뚰븳??
            }
        }

        private void MoveDiveAttack() // ?먰샇 ?대룞 ??Nexus濡?怨좎냽 ?뚯쭊?섎뒗 ?⑥닔
        {
            Vector3 offset = diveTargetPosition - transform.position; // ?꾩옱 ?꾩튂?먯꽌 ?뚯쭊 紐⑺몴?먭퉴吏??諛⑺뼢怨?嫄곕━瑜?怨꾩궛?쒕떎.

            if (offset.sqrMagnitude <= diveHitDistance * diveHitDistance) // ?뚯쭊 紐⑺몴?먯쓽 ?꾩갑 踰붿쐞 ?덉씠?쇰㈃
            {
                HitNexus(); // Nexus???쇳빐瑜??곸슜?섍퀬 ?ъ궗泥대? ?쒓굅?쒕떎.
                return;
            }

            movementDirection = offset.normalized; // ?뚯쭊 紐⑺몴??諛⑺뼢??湲몄씠瑜?1濡?留뚮뱺??

            transform.position = Vector3.MoveTowards(transform.position, diveTargetPosition, diveSpeed * Time.deltaTime); // Nexus 二쇰? 異⑸룎 吏?먯쑝濡?鍮좊Ⅴ寃??뚯쭊?쒕떎.
            transform.rotation = Quaternion.LookRotation(movementDirection, Vector3.up); // ?뚯쭊 諛⑺뼢??諛붾씪蹂닿쾶 ?쒕떎.

            Vector3 remainingOffset = diveTargetPosition - transform.position; // ?대룞 ???⑥? 嫄곕━瑜??ㅼ떆 怨꾩궛?쒕떎.

            if (remainingOffset.sqrMagnitude <= diveHitDistance * diveHitDistance) // ?대룞 ???꾩갑 踰붿쐞 ?덉뿉 ?ㅼ뼱?붾떎硫?
            {
                HitNexus(); // Nexus???쇳빐瑜??곸슜?섍퀬 ?ъ궗泥대? ?쒓굅?쒕떎.
            }
        }

        private bool TryHitNormalTarget() // Normal ?ъ궗泥댁쓽 Nexus ?꾩갑 ?щ?瑜??뺤씤?섎뒗 ?⑥닔
        {
            Vector3 offset = target.position - transform.position; // ?ъ궗泥댁? Nexus 以묒떖 ?ъ씠??嫄곕━瑜?怨꾩궛?쒕떎.

            if (offset.sqrMagnitude > hitDistance * hitDistance) // ?꾩쭅 Nexus ?꾩갑 踰붿쐞蹂대떎 硫?ㅻ㈃
            {
                return false; // ?꾩갑?섏? ?딆븯?ㅺ퀬 諛섑솚?쒕떎.
            }

            HitNexus(); // Nexus ?쇳빐? ?ъ궗泥??쒓굅瑜?泥섎━?쒕떎.
            return true; // Nexus???꾩갑?덈떎怨?諛섑솚?쒕떎.
        }

        private void HitNexus() // Nexus ?쇳빐瑜?泥섎━?섎뒗 ?⑥닔
        {
            if (target != null) // Nexus媛 ?꾩쭅 議댁옱?쒕떎硫?
            {
                NexusController.TryApplyDamage(target, nexusDamage); // Nexus 怨듯넻 ?쇳빐 API濡??쇳빐瑜??곸슜?쒕떎.
            }

            DestroyProjectile(); // ?쇳빐 泥섎━ ???ъ궗泥대? ?쒓굅?쒕떎.
        }

        private void DestroyProjectile() // ?ъ궗泥??쒓굅瑜???怨녹뿉??泥섎━?섎뒗 ?⑥닔
        {
            if (isDestroyed) // ?대? ?쒓굅 泥섎━媛 ?쒖옉?먮떎硫?
            {
                return; // Destroy瑜?以묐났 ?ㅽ뻾?섏? ?딅뒗??
            }

            isDestroyed = true; // ?쒓굅 ?곹깭濡???ν븳??
            Destroy(gameObject); // ?ъ궗泥?GameObject瑜??쒓굅?쒕떎.
        }
    }
}
