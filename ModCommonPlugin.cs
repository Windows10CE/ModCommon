using System;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using MonoMod.Cil;
using RoR2;

namespace ModCommon
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkModlistException]
    public class ModCommonPlugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.Windows10CE.ModCommon";
        public const string ModName = "ModCommon";
        public const string ModVer = "1.0.0";

        private static Harmony HarmonyInstance = new Harmony(ModGUID);
        private static bool dontDoNetwork = false;

        public void Awake()
        {
            // Set isModded flag to disable Trials and put players in another modded queue
            RoR2Application.isModded = true;

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.Keys.Contains("com.bepis.r2api"))
            {
                // Instead of doing our own network mod list, let r2api do it for us
                HarmonyInstance.Patch(AccessTools.Method("R2API.Utils.NetworkCompatibilityHandler:TryGetNetworkCompatibility"), postfix: new HarmonyMethod(AccessTools.Method(typeof(NetworkCompatabilityR2API), nameof(NetworkCompatabilityR2API.CatchNetworkCompatAttribute))));
                dontDoNetwork = true;
            } else
            {
                // r2api ILLine crashes if any other mod has applied the patch, so we avoid that :)
                HarmonyInstance.PatchAll(typeof(ILLine));
            }
        }

        public void Start()
        {
            if (dontDoNetwork)
                return;

            var networkModList = new List<string>();
            foreach (var mod in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                if (!mod.Instance.GetType().CustomAttributes.Any(x => x.AttributeType == typeof(NetworkModlistExceptionAttribute)))
                {
                    Logger.LogMessage($"Adding {mod.Metadata.GUID} to the network mod list...");
                    networkModList.Add(mod.Metadata.GUID + ";" + mod.Metadata.Version);
                }
            }

            NetworkModCompatibilityHelper.networkModList = NetworkModCompatibilityHelper.networkModList.Count() > 0 ? NetworkModCompatibilityHelper.networkModList.Union(networkModList) : networkModList;
        }
    }

    [HarmonyPatch]
    internal static class ILLine
    {
        [HarmonyILManipulator]
        [HarmonyPatch(typeof(StackTrace), "AddFrames")]
        internal static void ApplyILLine(ILContext il)
        {
            var cursor = new ILCursor(il);
            
            bool found = cursor.TryGotoNext(
                x => x.MatchCallOrCallvirt(typeof(StackFrame).GetMethod("GetFileLineNumber", BindingFlags.Instance | BindingFlags.Public))
            );
            if (!found)
                return;

            cursor.RemoveRange(2);
            cursor.EmitDelegate<Func<StackFrame, string>>(GetLineOrIL);
        }
        private static string GetLineOrIL(StackFrame instace)
        {
            var line = instace.GetFileLineNumber();
            if (line != StackFrame.OFFSET_UNKNOWN && line != 0)
            {
                return line.ToString();
            }

            return "IL_" + instace.GetILOffset().ToString("X4");
        }
    }

    internal static class NetworkCompatabilityR2API
    {
        private static MethodInfo SetCompatMethod = null;
        
        internal static void CatchNetworkCompatAttribute(Type baseUnityPluginType, ref object networkCompatibility)
        {
            if (SetCompatMethod is null)
                SetCompatMethod = networkCompatibility.GetType().GetProperty("CompatibilityLevel").GetSetMethod(true);
            if (baseUnityPluginType.CustomAttributes.Any(x => x.AttributeType == typeof(NetworkModlistExceptionAttribute)))
                SetCompatMethod.Invoke(networkCompatibility, new object[] { 0 });
        }
    }

    public class NetworkModlistExceptionAttribute : Attribute { }
}
