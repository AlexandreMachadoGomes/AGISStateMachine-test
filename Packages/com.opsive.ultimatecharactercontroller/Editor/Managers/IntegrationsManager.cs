/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
/// 
namespace Opsive.UltimateCharacterController.Editor.Managers
{
    using Opsive.Shared.Editor.Managers;


    /// <summary>
    /// Draws a list of all of the available integrations.
    /// </summary>
    [OrderedEditorItem("Integrations", 10)]
    public class IntegrationsManager : Shared.Editor.Managers.IntegrationsManager
    {
        protected override string IntegrationsURL => "https://opsive.com/asset/UltimateCharacterController/Version3IntegrationsList.txt";
    }
}