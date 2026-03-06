/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.Editor.Managers
{
    using Opsive.Shared.Editor.Managers;

    /// <summary>
    /// Shows a starting window with useful links.
    /// </summary>
    [OrderedEditorItem("Welcome", 0)]
    public class WelcomeScreenManager : Opsive.Shared.Editor.Managers.WelcomeScreenManager
    {
        protected override string AssetName => UltimateCharacterController.Utility.AssetInfo.Name;
        protected override string AssetVersion => UltimateCharacterController.Utility.AssetInfo.Version;
        protected override bool AddLargeDocumentationImage => true;

        /// <summary>
        /// Initializes the manager after deserialization.
        /// </summary>
        /// <param name="mainManagerWindow">A reference to the Main Manager Window.</param>
        public override void Initialize(MainManagerWindow mainManagerWindow)
        {
            base.Initialize(mainManagerWindow);
        }

        /// <summary>
        /// Returns the URL for the documentation page.
        /// </summary>
        /// <returns>The URL for the documentation page.</returns>
        protected override string GetDocumentationURL()
        {
            return "https://opsive.com/support/documentation/ultimate-character-controller";
        }

        /// <summary>
        /// Returns the URL for the videos page.
        /// </summary>
        /// <returns>The URL for the videos page.</returns>
        protected override string GetVideosURL()
        {
            switch (UltimateCharacterController.Utility.AssetInfo.Name) {
                case "Ultimate Character Controller":
                    return "https://opsive.com/videos/?pid=25992";
                case "Ultimate First Person Shooter":
                    return "https://opsive.com/videos/?pid=25993";
                case "Third Person Controller":
                    return "https://opsive.com/videos/?pid=25994";
            }
            return string.Empty;
        }

        /// <summary>
        /// Returns the URL for the asset page.
        /// </summary>
        /// <returns>The URL for the asset page.</returns>
        protected override string GetAssetURL()
        {
            switch (UltimateCharacterController.Utility.AssetInfo.Name) {
                case "Ultimate Character Controller":
                    return "https://assetstore.unity.com/packages/slug/233710";
                case "Ultimate First Person Shooter":
                    return "https://assetstore.unity.com/packages/slug/233711";
                case "Third Person Controller":
                    return "https://assetstore.unity.com/packages/slug/233712";
            }
            return string.Empty;
        }
    }
}