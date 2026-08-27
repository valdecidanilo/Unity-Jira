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
            field.labelElement.enableRichText = true;
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

        public static void ApplyLoaderRow(VisualElement row)
        {
            row.style.minHeight = 46;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.backgroundColor = SurfaceRaised;
            row.style.borderTopLeftRadius = 6;
            row.style.borderTopRightRadius = 6;
            row.style.borderBottomLeftRadius = 6;
            row.style.borderBottomRightRadius = 6;
            row.style.borderLeftWidth = 1;
            row.style.borderRightWidth = 1;
            row.style.borderTopWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftColor = Border;
            row.style.borderRightColor = Border;
            row.style.borderTopColor = Border;
            row.style.borderBottomColor = Border;
        }

        public static void ApplyLoaderSpinner(VisualElement spinner)
        {
            spinner.style.width = 18;
            spinner.style.minWidth = 18;
            spinner.style.height = 18;
            spinner.style.flexShrink = 0;
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
            dropdown.labelElement.enableRichText = true;
            dropdown.labelElement.style.color = TextSecondary;
            dropdown.labelElement.style.marginBottom = 5;
            dropdown.labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;

            ApplyDropdownParts(dropdown);
            dropdown.RegisterCallback<AttachToPanelEvent>(_ => ApplyDropdownParts(dropdown));
        }

        private static void ApplyDropdownParts(DropdownField dropdown)
        {
            VisualElement input = dropdown.Q<VisualElement>(
                className: "unity-base-popup-field__input");
            if (input != null)
            {
                input.style.height = 30;
                input.style.minHeight = 30;
                input.style.backgroundColor = SurfaceRaised;
                input.style.borderLeftWidth = 1;
                input.style.borderRightWidth = 1;
                input.style.borderTopWidth = 1;
                input.style.borderBottomWidth = 1;
                input.style.borderLeftColor = Border;
                input.style.borderRightColor = Border;
                input.style.borderTopColor = Border;
                input.style.borderBottomColor = Border;
                input.style.borderTopLeftRadius = 6;
                input.style.borderTopRightRadius = 6;
                input.style.borderBottomLeftRadius = 6;
                input.style.borderBottomRightRadius = 6;
                input.style.paddingLeft = 10;
                input.style.paddingRight = 8;
            }

            TextElement text = dropdown.Q<TextElement>(
                className: "unity-base-popup-field__text");
            if (text != null)
            {
                text.style.color = TextPrimary;
                text.style.fontSize = 11;
            }

            VisualElement arrow = dropdown.Q<VisualElement>(
                className: "unity-base-popup-field__arrow");
            if (arrow != null)
                arrow.style.unityBackgroundImageTintColor = Accent;
        }

        public static void ApplyDropdownPopup(VisualElement popup)
        {
            popup.style.position = Position.Absolute;
            popup.style.backgroundColor = new StyleColor(new Color32(35, 39, 46, 255));
            popup.style.paddingLeft = 8;
            popup.style.paddingRight = 8;
            popup.style.paddingTop = 8;
            popup.style.paddingBottom = 8;
            popup.style.borderLeftWidth = 1;
            popup.style.borderRightWidth = 1;
            popup.style.borderTopWidth = 1;
            popup.style.borderBottomWidth = 1;
            popup.style.borderLeftColor = new Color(Accent.r, Accent.g, Accent.b, 0.65f);
            popup.style.borderRightColor = new Color(Accent.r, Accent.g, Accent.b, 0.65f);
            popup.style.borderTopColor = new Color(Accent.r, Accent.g, Accent.b, 0.65f);
            popup.style.borderBottomColor = new Color(Accent.r, Accent.g, Accent.b, 0.65f);
            popup.style.borderTopLeftRadius = 8;
            popup.style.borderTopRightRadius = 8;
            popup.style.borderBottomLeftRadius = 8;
            popup.style.borderBottomRightRadius = 8;
        }

        public static void ApplyDropdownPopupCaption(Label label)
        {
            label.style.color = TextSecondary;
            label.style.fontSize = 10;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginLeft = 8;
            label.style.marginTop = 2;
            label.style.marginBottom = 3;
        }

        public static void ApplyDropdownPopupCurrent(Label label, StyleColor accent)
        {
            label.style.height = 28;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.backgroundColor = SurfaceRaised;
            label.style.color = accent;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.borderTopLeftRadius = 5;
            label.style.borderTopRightRadius = 5;
            label.style.borderBottomLeftRadius = 5;
            label.style.borderBottomRightRadius = 5;
        }

        public static void ApplyDropdownPopupDivider(VisualElement divider)
        {
            divider.style.height = 1;
            divider.style.backgroundColor = Border;
            divider.style.marginTop = 7;
            divider.style.marginBottom = 7;
        }

        public static void ApplyDropdownPopupItem(Button button)
        {
            button.style.height = 32;
            button.style.marginBottom = 2;
            button.style.paddingLeft = 10;
            button.style.paddingRight = 10;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.fontSize = 11;
            button.style.color = TextPrimary;
            button.style.backgroundColor = Color.clear;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
            button.RegisterCallback<PointerEnterEvent>(_ =>
                button.style.backgroundColor = new Color(Accent.r, Accent.g, Accent.b, 0.2f));
            button.RegisterCallback<PointerLeaveEvent>(_ =>
                button.style.backgroundColor = Color.clear);
        }

        public static void ApplyDropdownPopupEmpty(Label label)
        {
            label.style.paddingLeft = 10;
            label.style.paddingRight = 10;
            label.style.paddingTop = 9;
            label.style.paddingBottom = 9;
            label.style.color = TextSecondary;
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        public static void ApplyCloseButton(Button button)
        {
            button.style.width = 28;
            button.style.minWidth = 28;
            button.style.height = 28;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.marginLeft = 10;
            button.style.backgroundColor = new Color(Danger.r, Danger.g, Danger.b, 0.12f);
            button.style.color = Danger;
            button.style.fontSize = 17;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            Color closeBorder = new Color(Danger.r, Danger.g, Danger.b, 0.45f);
            button.style.borderLeftColor = closeBorder;
            button.style.borderRightColor = closeBorder;
            button.style.borderTopColor = closeBorder;
            button.style.borderBottomColor = closeBorder;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
        }

        public static void ApplyPriorityButton(Button button)
        {
            button.style.width = 30;
            button.style.minWidth = 30;
            button.style.height = 26;
            button.style.paddingLeft = 4;
            button.style.paddingRight = 4;
            button.style.marginLeft = 7;
            button.style.marginRight = 7;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.backgroundColor = SurfaceRaised;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftColor = Border;
            button.style.borderRightColor = Border;
            button.style.borderTopColor = Border;
            button.style.borderBottomColor = Border;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
        }

        public static void ApplyMultiline(TextField field)
        {
            field.multiline = true;
            field.labelElement.enableRichText = true;
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
            label.enableRichText = true;
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

        // --- Agent console -------------------------------------------------

        private static Color ToneColor(JiraTone tone)
        {
            switch (tone)
            {
                case JiraTone.Accent: return Accent;
                case JiraTone.Success: return Success;
                case JiraTone.Danger: return Danger;
                default: return TextSecondary;
            }
        }

        /// <summary>Compact status badge used for run state.</summary>
        public static void ApplyStatusPill(Label label, JiraTone tone)
        {
            Color color = ToneColor(tone);

            label.style.color = color;
            label.style.fontSize = 9;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingLeft = 7;
            label.style.paddingRight = 7;
            label.style.paddingTop = 2;
            label.style.paddingBottom = 2;
            label.style.marginRight = 8;
            label.style.borderTopLeftRadius = 9;
            label.style.borderTopRightRadius = 9;
            label.style.borderBottomLeftRadius = 9;
            label.style.borderBottomRightRadius = 9;
            label.style.borderLeftWidth = 1;
            label.style.borderRightWidth = 1;
            label.style.borderTopWidth = 1;
            label.style.borderBottomWidth = 1;
            label.style.borderLeftColor = color;
            label.style.borderRightColor = color;
            label.style.borderTopColor = color;
            label.style.borderBottomColor = color;
            label.style.flexShrink = 0;
        }

        /// <summary>Scroll region that holds the live transcript.</summary>
        public static void ApplyTranscriptScroll(ScrollView scroll)
        {
            scroll.style.maxHeight = 260;
            scroll.style.minHeight = 90;
            scroll.style.marginTop = 8;
            scroll.style.backgroundColor = Background;
            scroll.style.borderTopLeftRadius = 6;
            scroll.style.borderTopRightRadius = 6;
            scroll.style.borderBottomLeftRadius = 6;
            scroll.style.borderBottomRightRadius = 6;
            scroll.style.borderLeftWidth = 1;
            scroll.style.borderRightWidth = 1;
            scroll.style.borderTopWidth = 1;
            scroll.style.borderBottomWidth = 1;
            scroll.style.borderLeftColor = Border;
            scroll.style.borderRightColor = Border;
            scroll.style.borderTopColor = Border;
            scroll.style.borderBottomColor = Border;
            scroll.style.paddingLeft = 8;
            scroll.style.paddingRight = 8;
            scroll.style.paddingTop = 6;
            scroll.style.paddingBottom = 6;
        }

        /// <summary>One transcript line: a tone-colored tag plus its text.</summary>
        public static void ApplyTranscriptTag(Label label, JiraTone tone)
        {
            label.style.color = ToneColor(tone);
            label.style.fontSize = 9;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.width = 74;
            label.style.flexShrink = 0;
            label.style.marginRight = 6;
        }

        public static void ApplyTranscriptText(Label label)
        {
            label.style.color = TextPrimary;
            label.style.fontSize = 10;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexGrow = 1;
            label.style.flexShrink = 1;
        }

        public static void ApplyTranscriptRow(VisualElement row)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 3;
        }

        /// <summary>Monospace-ish block for a final answer or an error dump.</summary>
        public static void ApplyResultBlock(Label label, bool isError)
        {
            label.style.color = isError ? Danger : TextPrimary;
            label.style.fontSize = 10;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.backgroundColor = Background;
            label.style.paddingLeft = 9;
            label.style.paddingRight = 9;
            label.style.paddingTop = 8;
            label.style.paddingBottom = 8;
            label.style.marginTop = 6;
            label.style.borderTopLeftRadius = 6;
            label.style.borderTopRightRadius = 6;
            label.style.borderBottomLeftRadius = 6;
            label.style.borderBottomRightRadius = 6;
            label.style.borderLeftWidth = 2;
            label.style.borderLeftColor = isError ? Danger : Accent;
        }

        /// <summary>Selectable row in the run history list.</summary>
        public static void ApplyRunRow(Button button, bool selected)
        {
            button.style.height = 26;
            button.style.marginBottom = 3;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.fontSize = 10;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.color = selected ? TextPrimary : TextSecondary;
            button.style.backgroundColor = selected ? SurfaceRaised : Surface;
            button.style.borderTopLeftRadius = 5;
            button.style.borderTopRightRadius = 5;
            button.style.borderBottomLeftRadius = 5;
            button.style.borderBottomRightRadius = 5;
            button.style.borderLeftWidth = selected ? 2 : 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftColor = selected ? Accent : Border;
            button.style.borderRightColor = Border;
            button.style.borderTopColor = Border;
            button.style.borderBottomColor = Border;
        }

        // --- Agent chat ------------------------------------------------------

        /// <summary>Single compact row of controls, used as the agent tab's toolbar.</summary>
        public static void ApplyToolbar(VisualElement bar)
        {
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.backgroundColor = Surface;
            bar.style.paddingLeft = 8;
            bar.style.paddingRight = 8;
            bar.style.paddingTop = 6;
            bar.style.paddingBottom = 6;
            bar.style.marginBottom = 6;
            bar.style.borderTopLeftRadius = 6;
            bar.style.borderTopRightRadius = 6;
            bar.style.borderBottomLeftRadius = 6;
            bar.style.borderBottomRightRadius = 6;
            bar.style.borderLeftWidth = 1;
            bar.style.borderRightWidth = 1;
            bar.style.borderTopWidth = 1;
            bar.style.borderBottomWidth = 1;
            bar.style.borderLeftColor = Border;
            bar.style.borderRightColor = Border;
            bar.style.borderTopColor = Border;
            bar.style.borderBottomColor = Border;
        }

        /// <summary>Small button that fits inside a toolbar row.</summary>
        public static void ApplyToolbarButton(Button button, bool primary)
        {
            button.style.height = 22;
            button.style.minWidth = 0;
            button.style.marginLeft = 0;
            button.style.marginRight = 4;
            button.style.marginTop = 1;
            button.style.marginBottom = 1;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.fontSize = 10;
            button.style.unityFontStyleAndWeight = primary ? FontStyle.Bold : FontStyle.Normal;
            button.style.color = primary ? Accent : TextSecondary;
            button.style.backgroundColor = primary
                ? new Color(Accent.r, Accent.g, Accent.b, 0.16f)
                : SurfaceRaised;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            Color border = primary ? new Color(Accent.r, Accent.g, Accent.b, 0.45f) : Border;
            button.style.borderLeftColor = border;
            button.style.borderRightColor = border;
            button.style.borderTopColor = border;
            button.style.borderBottomColor = border;
        }

        /// <summary>Inline text inside a toolbar: a status, a path, a counter.</summary>
        public static void ApplyToolbarText(Label label, JiraTone tone)
        {
            label.style.color = tone == JiraTone.Neutral ? TextSecondary : ToneColor(tone);
            label.style.fontSize = 10;
            label.style.marginRight = 10;
            label.style.flexShrink = 1;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
        }

        /// <summary>Track of the token meter. The fill is a child sized by percentage.</summary>
        public static void ApplyMeterTrack(VisualElement track)
        {
            track.style.height = 5;
            track.style.width = 110;
            track.style.marginRight = 8;
            track.style.backgroundColor = Background;
            track.style.borderTopLeftRadius = 3;
            track.style.borderTopRightRadius = 3;
            track.style.borderBottomLeftRadius = 3;
            track.style.borderBottomRightRadius = 3;
            track.style.overflow = Overflow.Hidden;
            track.style.flexShrink = 0;
        }

        public static void ApplyMeterFill(VisualElement fill, float fraction, JiraTone tone)
        {
            fill.style.height = 5;
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            fill.style.backgroundColor = ToneColor(tone);
            fill.style.borderTopLeftRadius = 3;
            fill.style.borderTopRightRadius = 3;
            fill.style.borderBottomLeftRadius = 3;
            fill.style.borderBottomRightRadius = 3;
        }

        /// <summary>The chat surface. Grows with the window instead of a fixed box.</summary>
        public static void ApplyChatScroll(ScrollView scroll)
        {
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 220;
            scroll.style.backgroundColor = Background;
            scroll.style.marginBottom = 6;
            scroll.style.borderTopLeftRadius = 6;
            scroll.style.borderTopRightRadius = 6;
            scroll.style.borderBottomLeftRadius = 6;
            scroll.style.borderBottomRightRadius = 6;
            scroll.style.borderLeftWidth = 1;
            scroll.style.borderRightWidth = 1;
            scroll.style.borderTopWidth = 1;
            scroll.style.borderBottomWidth = 1;
            scroll.style.borderLeftColor = Border;
            scroll.style.borderRightColor = Border;
            scroll.style.borderTopColor = Border;
            scroll.style.borderBottomColor = Border;
            scroll.style.paddingLeft = 8;
            scroll.style.paddingRight = 8;
            scroll.style.paddingTop = 8;
            scroll.style.paddingBottom = 8;
        }

        /// <summary>A chat message. The developer's turns are accented and indented.</summary>
        public static void ApplyBubble(Label label, bool fromUser)
        {
            label.style.color = TextPrimary;
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.backgroundColor = fromUser
                ? new Color(Accent.r, Accent.g, Accent.b, 0.16f)
                : SurfaceRaised;
            label.style.paddingLeft = 10;
            label.style.paddingRight = 10;
            label.style.paddingTop = 7;
            label.style.paddingBottom = 7;
            label.style.marginTop = 2;
            label.style.marginBottom = 2;
            label.style.marginLeft = fromUser ? 40 : 0;
            label.style.marginRight = fromUser ? 0 : 40;
            label.style.borderTopLeftRadius = 8;
            label.style.borderTopRightRadius = 8;
            label.style.borderBottomLeftRadius = fromUser ? 8 : 2;
            label.style.borderBottomRightRadius = fromUser ? 2 : 8;
        }

        /// <summary>Caption above a message: who spoke and when.</summary>
        public static void ApplyBubbleCaption(Label label, bool fromUser)
        {
            label.style.color = fromUser ? Accent : TextSecondary;
            label.style.fontSize = 9;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            label.style.marginBottom = 1;
            label.style.marginLeft = fromUser ? 40 : 0;
        }

        /// <summary>
        /// The one-line summary of what the agent is doing, which expands into the
        /// full step log.
        /// </summary>
        public static void ApplyActivityToggle(Button button, bool expanded)
        {
            button.style.height = 20;
            button.style.marginLeft = 0;
            button.style.marginRight = 40;
            button.style.marginTop = 2;
            button.style.marginBottom = 2;
            button.style.paddingLeft = 8;
            button.style.paddingRight = 8;
            button.style.fontSize = 9;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.color = TextSecondary;
            button.style.backgroundColor = expanded ? SurfaceRaised : Color.clear;
            button.style.borderTopLeftRadius = 4;
            button.style.borderTopRightRadius = 4;
            button.style.borderBottomLeftRadius = 4;
            button.style.borderBottomRightRadius = 4;
            button.style.borderLeftWidth = 1;
            button.style.borderRightWidth = 1;
            button.style.borderTopWidth = 1;
            button.style.borderBottomWidth = 1;
            button.style.borderLeftColor = Border;
            button.style.borderRightColor = Border;
            button.style.borderTopColor = Border;
            button.style.borderBottomColor = Border;
        }

        /// <summary>Expanded step log under an activity toggle.</summary>
        public static void ApplyActivityLog(VisualElement log)
        {
            log.style.marginLeft = 8;
            log.style.marginRight = 40;
            log.style.marginBottom = 4;
            log.style.paddingLeft = 8;
            log.style.paddingTop = 4;
            log.style.paddingBottom = 4;
            log.style.borderLeftWidth = 1;
            log.style.borderLeftColor = Border;
        }

        /// <summary>Small metadata line under a turn: duration, tokens, cost.</summary>
        public static void ApplyTurnMeta(Label label)
        {
            label.style.color = TextSecondary;
            label.style.fontSize = 9;
            label.style.marginTop = 2;
            label.style.marginBottom = 6;
            label.style.whiteSpace = WhiteSpace.Normal;
        }

        /// <summary>The message box at the bottom of the chat.</summary>
        public static void ApplyComposer(TextField field)
        {
            field.multiline = true;
            field.style.flexGrow = 1;
            field.style.minHeight = 54;
            field.style.marginBottom = 0;
            field.style.marginRight = 6;
            field.style.color = TextPrimary;
            field.style.whiteSpace = WhiteSpace.Normal;
        }

        /// <summary>Send button beside the composer.</summary>
        public static void ApplySendButton(Button button)
        {
            button.style.height = 54;
            button.style.width = 92;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.backgroundColor = Accent;
            button.style.color = Color.white;
            button.style.fontSize = 11;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
        }

        /// <summary>Small pill carrying a value, such as the linked issue key.</summary>
        public static void ApplyChip(Label label, JiraTone tone)
        {
            Color color = ToneColor(tone);
            label.style.color = color;
            label.style.fontSize = 9;
            label.style.marginRight = 6;
            label.style.paddingLeft = 6;
            label.style.paddingRight = 6;
            label.style.paddingTop = 1;
            label.style.paddingBottom = 1;
            label.style.backgroundColor = new Color(color.r, color.g, color.b, 0.14f);
            label.style.borderTopLeftRadius = 4;
            label.style.borderTopRightRadius = 4;
            label.style.borderBottomLeftRadius = 4;
            label.style.borderBottomRightRadius = 4;
            label.style.flexShrink = 0;
        }

        /// <summary>Compact dropdown for a toolbar, without its label column.</summary>
        public static void ApplyMiniDropdown(DropdownField dropdown)
        {
            ApplyDropdown(dropdown);
            dropdown.style.marginBottom = 0;
            dropdown.style.marginRight = 6;
            dropdown.style.minWidth = 132;
            dropdown.style.flexGrow = 0;

            if (dropdown.labelElement != null)
                dropdown.labelElement.style.display = DisplayStyle.None;
        }

        /// <summary>Horizontal container for a group of buttons.</summary>
        public static void ApplyButtonRow(VisualElement row)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;
            row.style.flexWrap = Wrap.Wrap;
        }
    }

    /// <summary>Semantic color roles, so callers never name a raw color.</summary>
    internal enum JiraTone
    {
        Neutral,
        Accent,
        Success,
        Danger
    }
}
