using Bloxstrap.Enums.FlagPresets;

namespace Bloxstrap
{
    public class FastFlagManager : JsonManager<Dictionary<string, object>>
    {
        public override string ClassName => nameof(FastFlagManager);

        public override string LOG_IDENT_CLASS => ClassName;
        
        public override string FileLocation => Path.Combine(Paths.Modifications, "ClientSettings\\ClientAppSettings.json");

        public bool Changed => !OriginalProp.SequenceEqual(Prop);

        public static IReadOnlyDictionary<string, string> PresetFlags = new Dictionary<string, string>
        {
            { "Network.Log", "FLogNetwork" },

            { "Rendering.Framerate", "DFIntTaskSchedulerTargetFps" },
            { "Rendering.ManualFullscreen", "FFlagHandleAltEnterFullscreenManually" },
            { "Rendering.DisableScaling", "DFFlagDisableDPIScale" },
            { "Rendering.MSAA", "FIntDebugForceMSAASamples" },
            { "Rendering.DisablePostFX", "FFlagDisablePostFx" },
            { "Rendering.ShadowIntensity", "FIntRenderShadowIntensity" },

            { "Rendering.Mode.D3D11", "FFlagDebugGraphicsPreferD3D11" },
            { "Rendering.Mode.D3D10", "FFlagDebugGraphicsPreferD3D11FL10" },

            { "Rendering.Lighting.Voxel", "DFFlagDebugRenderForceTechnologyVoxel" },
            { "Rendering.Lighting.ShadowMap", "FFlagDebugForceFutureIsBrightPhase2" },
            { "Rendering.Lighting.Future", "FFlagDebugForceFutureIsBrightPhase3" },

            { "Rendering.TextureQuality.OverrideEnabled", "DFFlagTextureQualityOverrideEnabled" },
            { "Rendering.TextureQuality.Level", "DFIntTextureQualityOverride" },
            { "Rendering.TerrainTextureQuality", "FIntTerrainArraySliceSize" },

            { "UI.FontSize", "FIntFontSizePadding" },

            { "UI.FullscreenTitlebarDelay", "FIntFullscreenTitleBarTriggerDelayMillis" },
            
            //{ "UI.Menu.Style.V2Rollout", "FIntNewInGameMenuPercentRollout3" },
            //{ "UI.Menu.Style.EnableV4.1", "FFlagEnableInGameMenuControls" },
            //{ "UI.Menu.Style.EnableV4.2", "FFlagEnableInGameMenuModernization" },
            //{ "UI.Menu.Style.EnableV4Chrome", "FFlagEnableInGameMenuChrome" },
            //{ "UI.Menu.Style.ReportButtonCutOff", "FFlagFixReportButtonCutOff" },


            //{ "UI.Menu.Style.ABTest.1", "FFlagEnableMenuControlsABTest" },
            //{ "UI.Menu.Style.ABTest.2", "FFlagEnableV3MenuABTest3" },
            //{ "UI.Menu.Style.ABTest.3", "FFlagEnableInGameMenuChromeABTest3" },
            //{ "UI.Menu.Style.ABTest.4", "FFlagEnableInGameMenuChromeABTest4" }
        };

        public static IReadOnlyDictionary<RenderingMode, string> RenderingModes => new Dictionary<RenderingMode, string>
        {
            { RenderingMode.Default, "None" },
            { RenderingMode.D3D11, "D3D11" },
            { RenderingMode.D3D10, "D3D10" },
        };

        public static IReadOnlyDictionary<LightingMode, string> LightingModes => new Dictionary<LightingMode, string>
        {
            { LightingMode.Default, "None" },
            { LightingMode.Voxel, "Voxel" },
            { LightingMode.ShadowMap, "ShadowMap" },
            { LightingMode.Future, "Future" }
        };

        public static IReadOnlyDictionary<MSAAMode, string?> MSAAModes => new Dictionary<MSAAMode, string?>
        {
            { MSAAMode.Default, null },
            { MSAAMode.x1, "1" },
            { MSAAMode.x2, "2" },
            { MSAAMode.x4, "4" }
        };

        public static IReadOnlyDictionary<TextureQuality, string?> TextureQualityLevels => new Dictionary<TextureQuality, string?>
        {
            { TextureQuality.Default, null },
            { TextureQuality.Level0, "0" },
            { TextureQuality.Level1, "1" },
            { TextureQuality.Level2, "2" },
            { TextureQuality.Level3, "3" },
        };

        // this is one hell of a dictionary definition lmao
        // since these all set the same flags, wouldn't making this use bitwise operators be better?
        //public static IReadOnlyDictionary<InGameMenuVersion, Dictionary<string, string?>> IGMenuVersions => new Dictionary<InGameMenuVersion, Dictionary<string, string?>>
        //{
        //    {
        //        InGameMenuVersion.Default,
        //        new Dictionary<string, string?>
        //        {
        //            { "V2Rollout", null },
        //            { "EnableV4", null },
        //            { "EnableV4Chrome", null },
        //            { "ABTest", null },
        //            { "ReportButtonCutOff", null }
        //        }
        //    },

        //    {
        //        InGameMenuVersion.V1,
        //        new Dictionary<string, string?>
        //        {
        //            { "V2Rollout", "0" },
        //            { "EnableV4", "False" },
        //            { "EnableV4Chrome", "False" },
        //            { "ABTest", "False" },
        //            { "ReportButtonCutOff", "False" }
        //        }
        //    },

        //    {
        //        InGameMenuVersion.V2,
        //        new Dictionary<string, string?>
        //        {
        //            { "V2Rollout", "100" },
        //            { "EnableV4", "False" },
        //            { "EnableV4Chrome", "False" },
        //            { "ABTest", "False" },
        //            { "ReportButtonCutOff", null }
        //        }
        //    },

        //    {
        //        InGameMenuVersion.V4,
        //        new Dictionary<string, string?>
        //        {
        //            { "V2Rollout", "0" },
        //            { "EnableV4", "True" },
        //            { "EnableV4Chrome", "False" },
        //            { "ABTest", "False" },
        //            { "ReportButtonCutOff", null }
        //        }
        //    },

        //    {
        //        InGameMenuVersion.V4Chrome,
        //        new Dictionary<string, string?>
        //        {
        //            { "V2Rollout", "0" },
        //            { "EnableV4", "True" },
        //            { "EnableV4Chrome", "True" },
        //            { "ABTest", "False" },
        //            { "ReportButtonCutOff", null }
        //        }
        //    }
        //};

        // all fflags are stored as strings
        // to delete a flag, set the value as null
        public void SetValue(string key, object? value)
        {
            const string LOG_IDENT = "FastFlagManager::SetValue";

            if (value is null)
            {
                if (Prop.ContainsKey(key))
                    App.Logger.WriteLine(LOG_IDENT, $"Deletion of '{key}' is pending");

                Prop.Remove(key);
            }
            else
            {
                if (Prop.ContainsKey(key))
                {
                    // 这里以前比的是 key 和值，永远不相等，早退从来没生效过
                    if (Prop[key]?.ToString() == value.ToString())
                        return;

                    App.Logger.WriteLine(LOG_IDENT, $"Changing of '{key}' from '{Prop[key]}' to '{value}' is pending");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Setting of '{key}' to '{value}' is pending");
                }

                Prop[key] = value.ToString()!;
            }
        }

        // this returns null if the fflag doesn't exist
        public string? GetValue(string key)
        {
            // check if we have an updated change for it pushed first
            if (Prop.TryGetValue(key, out object? value) && value is not null)
                return value.ToString();

            return null;
        }

        public void SetPreset(string prefix, object? value)
        {
            foreach (var pair in PresetFlags.Where(x => x.Key.StartsWith(prefix)))
                SetValue(pair.Value, value);
        }

        public void SetPresetEnum(string prefix, string target, object? value)
        {
            foreach (var pair in PresetFlags.Where(x => x.Key.StartsWith(prefix)))
            {
                if (pair.Key.StartsWith($"{prefix}.{target}"))
                    SetValue(pair.Value, value);
                else
                    SetValue(pair.Value, null);
            }
        }

        public string? GetPreset(string name)
        {
            if (!PresetFlags.ContainsKey(name))
            {
                App.Logger.WriteLine("FastFlagManager::GetPreset", $"Could not find preset {name}");
                Debug.Assert(false, $"Could not find preset {name}");
                return null;
            }

            return GetValue(PresetFlags[name]);
        }

        public T GetPresetEnum<T>(IReadOnlyDictionary<T, string> mapping, string prefix, string value) where T : Enum
        {
            foreach (var pair in mapping)
            {
                if (pair.Value == "None")
                    continue;

                if (GetPreset($"{prefix}.{pair.Value}") == value)
                    return pair.Key;
            }

            return mapping.First().Key;
        }

        public override void Save()
        {
            // convert all flag values to strings before saving

            foreach (var pair in Prop)
                Prop[pair.Key] = pair.Value.ToString()!;

            base.Save();

            // clone the dictionary
            OriginalProp = new(Prop);
        }

        public override void Load(bool alertFailure = true)
        {
            base.Load(alertFailure);

            // 只在缺键（或值为空）的时候补默认值。
            // 以前是每次加载都无条件 SetPreset，用户手动改过的值会被直接覆盖掉
            if (String.IsNullOrEmpty(GetValue(PresetFlags["Network.Log"])))
                SetValue(PresetFlags["Network.Log"], "7");

            if (String.IsNullOrEmpty(GetValue(PresetFlags["Rendering.ManualFullscreen"])))
                SetValue(PresetFlags["Rendering.ManualFullscreen"], "False");

            // 补完默认值再克隆。顺序反了的话 Changed 一启动就是 true，
            // 每次关设置窗口都会问一遍要不要保存
            OriginalProp = new(Prop);
        }
    }
}
