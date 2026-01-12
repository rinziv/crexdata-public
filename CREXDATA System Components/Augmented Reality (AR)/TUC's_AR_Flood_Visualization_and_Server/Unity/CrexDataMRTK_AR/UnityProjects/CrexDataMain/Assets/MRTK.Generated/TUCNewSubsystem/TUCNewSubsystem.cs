// TODO: [Optional] Add copyright and license statement(s).

using MixedReality.Toolkit;
using MixedReality.Toolkit.Subsystems;
using UnityEngine;
using UnityEngine.Scripting;

namespace TUC.MRTK3.Subsystems
{
    [Preserve]
    [MRTKSubsystem(
        Name = "tuc.mrtk3.subsystems",
        DisplayName = "TUC NewSubsystem",
        Author = "TUC",
        ProviderType = typeof(TUCNewSubsystemProvider),
        SubsystemTypeOverride = typeof(TUCNewSubsystem),
        ConfigType = typeof(BaseSubsystemConfig))]
    public class TUCNewSubsystem : NewSubsystem
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            // Fetch subsystem metadata from the attribute.
            var cinfo = XRSubsystemHelpers.ConstructCinfo<TUCNewSubsystem, NewSubsystemCinfo>();

            if (!TUCNewSubsystem.Register(cinfo))
            {
                Debug.LogError($"Failed to register the {cinfo.Name} subsystem.");
            }
        }

        [Preserve]
        class TUCNewSubsystemProvider : Provider
        {

            #region INewSubsystem implementation

            // TODO: Add the provider implementation.

            #endregion NewSubsystem implementation
        }
    }
}
