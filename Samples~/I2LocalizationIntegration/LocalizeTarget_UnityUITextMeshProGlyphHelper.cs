using Rewired.Glyphs.UnityUI;

using TMPro;

using UnityEditor;

using UnityEngine;

// Requires I2 Localization + Rewired's TextMeshPro glyph helper (UnityUITextMeshProGlyphHelper).
#if TextMeshPro
namespace I2.Loc
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class LocalizeTarget_UnityUITextMeshProGlyphHelper : LocalizeTarget<UnityUITextMeshProGlyphHelper>
    {
        static LocalizeTarget_UnityUITextMeshProGlyphHelper() { AutoRegister(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoRegister()
        {
            LocalizationManager.RegisterTarget(new LocalizeTargetDesc_Type<UnityUITextMeshProGlyphHelper, LocalizeTarget_UnityUITextMeshProGlyphHelper>
            { Name = "UnityUITextMeshProGlyphHelper", Priority = 100 });
        }

        public override eTermType GetPrimaryTermType(Localize cmp) => eTermType.Text;
        public override eTermType GetSecondaryTermType(Localize cmp) => eTermType.TextMeshPFont;
        public override bool CanUseSecondaryTerm() => true;
        public override bool AllowMainTermToBeRTL() => true;
        public override bool AllowSecondTermToBeRTL() => false;

        public override void GetFinalTerms(Localize cmp, string Main, string Secondary, out string primaryTerm, out string secondaryTerm)
        {
            primaryTerm = mTarget ? mTarget.text : null;
            secondaryTerm = string.Empty;
        }

        public override void DoLocalize(Localize cmp, string mainTranslation, string secondaryTranslation)
        {
            if (mainTranslation != null && mTarget.text != mainTranslation)
                mTarget.text = mainTranslation;
        }
    }
}
#endif
