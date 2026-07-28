using UnityEngine;
using UnityEngine.UIElements;

namespace OxenteGames.JiraCommunication.UI
{
    internal static class JiraStyles
    {
        private const float HeaderHeight = 71f;
        private const float TabBarHeight = 39f;
        private const float BrandFooterHeight = 31f;

        private static readonly Color Background = new Color32(30, 33, 39, 255);
        private static readonly Color Surface = new Color32(40, 44, 52, 255);
        private static readonly Color SurfaceRaised = new Color32(47, 52, 61, 255);
        private static readonly Color Border = new Color32(67, 73, 84, 255);
        private static readonly Color TextPrimary = new Color32(238, 240, 244, 255);
        private static readonly Color TextSecondary = new Color32(173, 181, 194, 255);
        private static readonly Color Accent = new Color32(38, 132, 255, 255);
        private static readonly Color Success = new Color32(54, 179, 126, 255);
        private static readonly Color Danger = new Color32(255, 86, 86, 255);

        public static void ApplyWindow(VisualElement root)
        {
            root.style.backgroundColor = Background;
            root.style.color = TextPrimary;
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Column;
        }

        public static void ApplyHeader(VisualElement element)
        {
            SetFixedHeight(element, HeaderHeight);
            element.style.paddingLeft = 22;
            element.style.paddingRight = 22;
            element.style.paddingTop = 18;
            element.style.paddingBottom = 16;
            element.style.backgroundColor = Surface;
            element.style.borderBottomWidth = 1;
            element.style.borderBottomColor = Border;
        }

        public static void ApplyContentViewport(ScrollView scroll)
        {
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.flexBasis = 0;
            scroll.style.minHeight = 0;
        }

        public static void ApplyBrandFooter(VisualElement footer)
        {
            SetFixedHeight(footer, BrandFooterHeight);
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 22;
            footer.style.paddingRight = 22;
            footer.style.paddingTop = 8;
            footer.style.paddingBottom = 8;
            footer.style.backgroundColor = Surface;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = Border;
        }

        public static void ApplyTitle(Label title)
        {
            title.style.fontSize = 19;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextPrimary;
        }

        public static void ApplySubtitle(Label subtitle)
        {
            subtitle.style.fontSize = 12;
            subtitle.style.color = TextSecondary;
            subtitle.style.marginTop = 4;
            subtitle.style.whiteSpace = WhiteSpace.Normal;
        }

        public static void ApplyCard(VisualElement card)
        {
            card.style.backgroundColor = Surface;
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftColor = Border;
            card.style.borderRightColor = Border;
            card.style.borderTopColor = Border;
            card.style.borderBottomColor = Border;
            card.style.paddingLeft = 18;
            card.style.paddingRight = 18;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.marginBottom = 14;
        }

        public static void ApplySectionTitle(Label label)
        {
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 10;
            label.style.color = TextPrimary;
        }

        public static void ApplyField(TextField field)
        {
            field.style.marginBottom = 10;
            field.style.color = TextPrimary;
        }

        public static void ApplyPrimaryButton(Button button)
        {
            button.style.height = 34;
            button.style.backgroundColor = Accent;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
            button.style.marginRight = 8;
        }

        public static void ApplySecondaryButton(Button button)
        {
            button.style.height = 34;
            button.style.backgroundColor = SurfaceRaised;
            button.style.color = TextPrimary;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
            button.style.borderLeftColor = Border;
            button.style.borderRightColor = Border;
            button.style.borderTopColor = Border;
            button.style.borderBottomColor = Border;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
        }

        public static void ApplyStatus(Label status, bool success)
        {
            status.style.backgroundColor = success
                ? new Color(Success.r, Success.g, Success.b, 0.15f)
                : new Color(Danger.r, Danger.g, Danger.b, 0.15f);
            status.style.color = success ? Success : Danger;
            status.style.paddingLeft = 12;
            status.style.paddingRight = 12;
            status.style.paddingTop = 9;
            status.style.paddingBottom = 9;
            status.style.borderTopLeftRadius = 5;
            status.style.borderTopRightRadius = 5;
            status.style.borderBottomLeftRadius = 5;
            status.style.borderBottomRightRadius = 5;
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginTop = 12;
        }

        public static void ApplyMuted(Label label)
        {
            label.style.color = TextSecondary;
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        public static void ApplyTabBar(VisualElement bar)
        {
            SetFixedHeight(bar, TabBarHeight);
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.paddingLeft = 22;
            bar.style.paddingRight = 22;
            bar.style.backgroundColor = Surface;
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = Border;
        }

        private static void SetFixedHeight(VisualElement element, float height)
        {
            element.style.height = height;
            element.style.minHeight = height;
            element.style.maxHeight = height;
            element.style.flexGrow = 0;
            element.style.flexShrink = 0;
        }

        public static void ApplyTab(Button tab, bool active)
        {
            tab.style.height = 38;
            tab.style.paddingLeft = 16;
            tab.style.paddingRight = 16;
            tab.style.marginRight = 4;
            tab.style.marginLeft = 0;
            tab.style.marginTop = 0;
            tab.style.marginBottom = 0;
            tab.style.backgroundColor = Color.clear;
            tab.style.color = active ? TextPrimary : TextSecondary;
            tab.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            tab.style.borderLeftWidth = 0;
            tab.style.borderRightWidth = 0;
            tab.style.borderTopWidth = 0;
            tab.style.borderBottomWidth = 2;
            tab.style.borderBottomColor = active ? Accent : Color.clear;
            tab.style.borderTopLeftRadius = 0;
            tab.style.borderTopRightRadius = 0;
            tab.style.borderBottomLeftRadius = 0;
            tab.style.borderBottomRightRadius = 0;
        }

        public static void ApplyDropdown(DropdownField dropdown)
        {
            dropdown.style.marginBottom = 10;
            dropdown.style.color = TextPrimary;
        }

        public static void ApplyMultiline(TextField field)
        {
            field.multiline = true;
            field.style.flexDirection = FlexDirection.Column;
            field.style.marginBottom = 10;
            field.style.minHeight = 84;
            field.style.color = TextPrimary;
            field.style.whiteSpace = WhiteSpace.Normal;
            field.labelElement.style.marginBottom = 5;
            field.labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;
        }

        public static void ApplyLinkButton(Button button)
        {
            button.style.height = 30;
            button.style.marginTop = 10;
            button.style.backgroundColor = new Color(Accent.r, Accent.g, Accent.b, 0.18f);
            button.style.color = Accent;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
        }

        public static void ApplyFieldHint(Label label)
        {
            label.style.color = TextSecondary;
            label.style.fontSize = 10;
            label.style.marginTop = -4;
            label.style.marginBottom = 10;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        public static void ApplyDynamicFieldLabel(Label label)
        {
            label.style.color = TextPrimary;
            label.style.fontSize = 12;
            label.style.marginBottom = 5;
        }

        public static void ApplyInlineStatus(Label label, bool success)
        {
            label.style.color = success ? TextSecondary : Danger;
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        public static void ApplyDynamicOptions(VisualElement options)
        {
            options.style.backgroundColor = SurfaceRaised;
            options.style.paddingLeft = 10;
            options.style.paddingRight = 10;
            options.style.paddingTop = 8;
            options.style.paddingBottom = 6;
            options.style.marginBottom = 10;
            options.style.borderTopLeftRadius = 5;
            options.style.borderTopRightRadius = 5;
            options.style.borderBottomLeftRadius = 5;
            options.style.borderBottomRightRadius = 5;
        }

        public static void ApplyNestedCard(VisualElement card)
        {
            card.style.backgroundColor = SurfaceRaised;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 4;
            card.style.marginTop = 8;
            card.style.marginBottom = 4;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftColor = Border;
            card.style.borderRightColor = Border;
            card.style.borderTopColor = Border;
            card.style.borderBottomColor = Border;
        }

        public static void ApplyCompactButton(Button button, bool danger)
        {
            button.style.height = 26;
            button.style.minWidth = danger ? 30 : 104;
            button.style.paddingLeft = danger ? 8 : 10;
            button.style.paddingRight = danger ? 8 : 10;
            button.style.backgroundColor = danger
                ? new Color(Danger.r, Danger.g, Danger.b, 0.12f)
                : new Color(Accent.r, Accent.g, Accent.b, 0.16f);
            button.style.color = danger ? Danger : Accent;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            Color borderColor = danger
                ? new Color(Danger.r, Danger.g, Danger.b, 0.45f)
                : new Color(Accent.r, Accent.g, Accent.b, 0.45f);
            button.style.borderLeftColor = borderColor;
            button.style.borderRightColor = borderColor;
            button.style.borderTopColor = borderColor;
            button.style.borderBottomColor = borderColor;
        }

        /// <summary>Wraps two elements side by side, each taking half the width.</summary>
        public static VisualElement Row(VisualElement left, VisualElement right)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            left.style.flexGrow = 1;
            left.style.flexBasis = 0;
            left.style.marginRight = 6;

            right.style.flexGrow = 1;
            right.style.flexBasis = 0;
            right.style.marginLeft = 6;

            row.Add(left);
            row.Add(right);
            return row;
        }

        public static void ApplyGhostButton(Button button)
        {
            button.style.height = 26;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.backgroundColor = SurfaceRaised;
            button.style.color = TextSecondary;
            button.style.fontSize = 11;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftColor = Border;
            button.style.borderRightColor = Border;
            button.style.borderTopColor = Border;
            button.style.borderBottomColor = Border;
        }

        public static void ApplyNote(Label label)
        {
            label.style.color = TextSecondary;
            label.style.fontSize = 10;
            label.style.marginTop = 8;
            label.style.whiteSpace = WhiteSpace.Normal;
        }
    }
}
