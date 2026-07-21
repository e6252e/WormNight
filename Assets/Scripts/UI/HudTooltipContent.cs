using System;

namespace TeamProject01.Gameplay
{
    [Serializable]
    public sealed class HudTooltipContent
    {
        public string Title;
        public string Body;
        public string Footer;

        public bool HasAnyText =>
            !string.IsNullOrWhiteSpace(Title)
            || !string.IsNullOrWhiteSpace(Body)
            || !string.IsNullOrWhiteSpace(Footer);

        public void Set(string title, string body, string footer)
        {
            Title = title;
            Body = body;
            Footer = footer;
        }
    }
}
