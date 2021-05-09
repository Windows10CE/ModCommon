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
    public class ModCommonPlugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.Windows10CE.ModCommon";
        public const string ModName = "ModCommon";
        public const string ModVer = "2.0.0";

        private static Harmony HarmonyInstance = new Harmony(ModGUID);

        public void Awake()
        {
            // Set isModded flag to disable Trials and put players in another modded queue
            RoR2Application.isModded = true;

            // Provide barebones language support
            HarmonyInstance.PatchAll(typeof(LanguageTokens));

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.Keys.Contains("com.bepis.r2api"))
            {
                // Keep ourself off the modlist
                HarmonyInstance.Patch(AccessTools.Method("R2API.Utils.NetworkCompatibilityHandler:TryGetNetworkCompatibility"), postfix: new HarmonyMethod(AccessTools.Method(typeof(NetworkCompatabilityR2API), nameof(NetworkCompatabilityR2API.CatchNetworkCompatAttribute))));
            } else
            {
                // r2api ILLine crashes if any other mod has applied the patch, so we avoid that :) also fixes console
                HarmonyInstance.PatchAll(typeof(SmallPatches));
            }
        }

        public void Start()
        {
            var networkModList = new List<string>();
            foreach (var mod in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                if (mod.Instance.GetType().CustomAttributes.Any(x => x.AttributeType == typeof(NetworkModlistIncludeAttribute)))
                {
                    Logger.LogMessage($"Adding {mod.Metadata.GUID} to the network mod list...");
                    networkModList.Add(mod.Metadata.GUID + ";" + mod.Metadata.Version);
                }
            }

            NetworkModCompatibilityHelper.networkModList = NetworkModCompatibilityHelper.networkModList.Concat(networkModList).Distinct();
        }
    }

    [HarmonyPatch]
    public static class LanguageTokens
    {
        private static Dictionary<string, string> tokens = new Dictionary<string, string>();

        public static void Add(string token, string val)
        {
            if (!tokens.ContainsKey(token))
                tokens.Add(token, val);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Language), nameof(Language.GetLocalizedStringByToken))]
        internal static void GetTokenPostfix(string token, ref string __result)
        {
            if (tokens.TryGetValue(token, out string val))
                __result = val;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Language), nameof(Language.TokenIsRegistered))]
        internal static void TokenExistsPostfix(string token, ref bool __result)
        {
            __result |= tokens.ContainsKey(token);
        }
    }

    [HarmonyPatch]
    internal static class SmallPatches
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnitySystemConsoleRedirector), nameof(UnitySystemConsoleRedirector.Redirect))]
        internal static bool StopConsoleRedirect() => false;
    }

    internal static class NetworkCompatabilityR2API
    {
        internal static void CatchNetworkCompatAttribute(Type baseUnityPluginType, ref object networkCompatibility)
        {
            if (baseUnityPluginType == typeof(ModCommonPlugin))
                networkCompatibility.GetType().GetProperty("CompatibilityLevel").GetSetMethod(true).Invoke(networkCompatibility, new object[] { 0 });
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class NetworkModlistIncludeAttribute : Attribute { }
}
