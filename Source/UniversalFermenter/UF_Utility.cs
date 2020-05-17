﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RimWorld;
using Verse;

namespace UniversalFermenter
{
    [StaticConstructorOnStartup]
    public static class UF_Utility
    {
        public static List<CompUniversalFermenter> comps = new List<CompUniversalFermenter>();

        public static List<UF_Process> allUFProcesses = new List<UF_Process>();

        public static Dictionary<UF_Process, Command_Action> processGizmos = new Dictionary<UF_Process, Command_Action>();
        public static Dictionary<QualityCategory, Command_Action> qualityGizmos = new Dictionary<QualityCategory, Command_Action>();

        public static Dictionary<UF_Process, Material> processMaterials = new Dictionary<UF_Process, Material>();
        public static Dictionary<QualityCategory, Material> qualityMaterials = new Dictionary<QualityCategory, Material>();
        
        static UF_Utility()
        {
            CheckForErrors();
            RecacheAll();
        }

        public static void CheckForErrors()
        {
            bool sendWarning = false;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("<-- Universal Fermenter Errors -->");
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompUniversalFermenter)))) //we grab every thingDef that has the UF comp
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompUniversalFermenter)) is CompProperties_UniversalFermenter compUF)
                {
                    if (!compUF.products.NullOrEmpty()) //if anyone uses the outdated "products" field we log a warning and copy the list to processes
                    {
                        stringBuilder.AppendLine("Universal Fermenter: ThingDef '" + thingDef.defName + "' uses outdated field 'products', please rename to 'processes'.");
                        compUF.processes.AddRange(compUF.products);
                        sendWarning = true;
                    }
                    if (compUF.processes.Any(p => p.thingDef == null || p.ingredientFilter.AllowedThingDefs.EnumerableNullOrEmpty()))
                    {
                        stringBuilder.AppendLine("ThingDef '" + thingDef.defName + "' has processes with no product or no filter. These fields are required.");
                        compUF.processes.RemoveAll(p => p.thingDef == null || p.ingredientFilter.AllowedThingDefs.EnumerableNullOrEmpty());
                        sendWarning = true;
                    }
                }
            }
            if (sendWarning)
            {
                Log.Warning(stringBuilder.ToString().TrimEndNewlines());
            }
        }

        public static void RecacheAll() //Gets called in constructor and in writeSettings
        {
            RecacheProcessGizmos();
            RecacheProcessMaterials();
            RecacheQualityGizmos();
        }

        public static void RecacheProcessGizmos()
        {
            allUFProcesses.Clear();
            processGizmos.Clear();
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompUniversalFermenter)))) //we grab every thingDef that has the UF comp
            {
                if (thingDef.comps.Find(c => c.compClass == typeof(CompUniversalFermenter)) is CompProperties_UniversalFermenter compUF)
                {
                    allUFProcesses.AddRange(compUF.processes); //adds the processes to a list so we have a full list of all processes
                    foreach (UF_Process process in compUF.processes) //we loop again to make a gizmo for each process, now that we have a complete FloatMenuOption list
                    {
                        Command_Process command_Process = new Command_Process
                        {
                            defaultLabel = process.thingDef.label,
                            defaultDesc = "UF_NextDesc".Translate(process.thingDef.label, IngredientFilterSummary(process.ingredientFilter)),
                            activateSound = SoundDefOf.Tick_Tiny,
                            icon = GetIcon(process.thingDef, UF_Settings.singleItemIcon),
                            processToTarget = process,
                            processOptions = compUF.processes
                            
                        };
                        command_Process.action = () =>
                        {
                            FloatMenu floatMenu = new FloatMenu(command_Process.RightClickFloatMenuOptions.ToList())
                            {
                                vanishIfMouseDistant = true,
                            };
                            Find.WindowStack.Add(floatMenu);
                        };
                        processGizmos.Add(process, command_Process);
                    }
                }
            }
        }

        public static void RecacheProcessMaterials()
        {
            processMaterials.Clear();
            foreach (UF_Process process in allUFProcesses)
            {
                Texture2D icon = GetIcon(process.thingDef, UF_Settings.singleItemIcon);
                Material mat = MaterialPool.MatFrom(icon);
                processMaterials.Add(process, mat);
            }
            qualityMaterials.Clear();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Texture2D icon = ContentFinder<Texture2D>.Get("UI/QualityIcons/" + quality.ToString());
                Material mat = MaterialPool.MatFrom(icon);
                qualityMaterials.Add(quality, mat);
            }
        }

        public static void RecacheQualityGizmos()
        {
            qualityGizmos.Clear();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Command_Quality command_Quality = new Command_Quality
                {
                    defaultLabel = quality.GetLabel().CapitalizeFirst(),
                    defaultDesc = "UF_SetQualityDesc".Translate(),
                    activateSound = SoundDefOf.Tick_Tiny,
                    icon = (Texture2D)qualityMaterials[quality].mainTexture,
                    qualityToTarget = quality
                };
                command_Quality.action = () =>
                {
                    FloatMenu floatMenu = new FloatMenu(command_Quality.RightClickFloatMenuOptions.ToList())
                    {
                        vanishIfMouseDistant = true,
                    };
                    Find.WindowStack.Add(floatMenu);
                };
                qualityGizmos.Add(quality, command_Quality);
            }
        }

        public static Command_Action DispSpeeds = new Command_Action()
        {
            defaultLabel = "DEBUG: Display Speed Factors",
            defaultDesc = "Display the current sun, rain, snow and wind speed factors and how much of the building is covered by roof.",
            activateSound = SoundDefOf.Tick_Tiny,
            action = () => 
            {
                foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                {
                    CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                    if (comp != null)
                    {
                        Log.Message(comp.parent.ToString() + ": " +
                              "sun: " + comp.CurrentSunFactor.ToString("0.00") +
                              "| rain: " + comp.CurrentRainFactor.ToString("0.00") +
                              "| snow: " + comp.CurrentSnowFactor.ToString("0.00") +
                              "| wind: " + comp.CurrentWindFactor.ToString("0.00") +
                              "| roofed: " + comp.RoofCoverage.ToString("0.00"));
                    }
                }
            }
        };
        public static Command_Action DevFinish = new Command_Action()
        {
            defaultLabel = "DEBUG: Finish",
            activateSound = SoundDefOf.Tick_Tiny,
            action = () => 
            {
                foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                {
                    CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                    if (comp != null)
                    {
                        if (comp.CurrentProcess.usesQuality)
                        {
                            comp.ProgressTicks = Mathf.RoundToInt(comp.DaysToReachTargetQuality * GenDate.TicksPerDay);
                        }
                        else
                        {
                            comp.ProgressTicks = Mathf.RoundToInt(comp.CurrentProcess.processDays * GenDate.TicksPerDay);
                        }
                    }
                }
            },
        };

        public static Command_Action AgeOneDay = new Command_Action()
        {
            defaultLabel = "DEBUG: Age One Day",
            activateSound = SoundDefOf.Tick_Tiny,
            action = () =>
            {
                foreach (Thing thing in Find.Selector.SelectedObjects.OfType<Thing>())
                {
                    CompUniversalFermenter comp = thing.TryGetComp<CompUniversalFermenter>();
                    if (comp != null)
                    {
                        comp.ProgressTicks += GenDate.TicksPerDay;
                    }
                }
            },
        };

        public static string IngredientFilterSummary(ThingFilter thingFilter)
        {
            return thingFilter.Summary;
        }

        public static string VowelTrim(string str, int limit)
        {
            int vowelsToRemove = str.Length - limit;
            for (int i = str.Length - 1; i > 0; i--)
            {
                if (vowelsToRemove <= 0)
                    break;

                if (IsVowel(str[i]))
                {
                    if (str[i - 1] == ' ')
                    {
                        continue;
                    }
                    else
                    {
                        str = str.Remove(i, 1);
                        vowelsToRemove--;
                    }
                }
            }

            if (str.Length > limit)
            {
                str = str.Remove(limit - 2) + "..";
            }

            return str;
        }

        public static bool IsVowel(char c)
        {
            var vowels = new HashSet<char> { 'a', 'e', 'i', 'o', 'u' };
            return vowels.Contains(c);
        }

        // Try to get a texture of a thingDef; If not found, use LaunchReport icon
        public static Texture2D GetIcon(ThingDef thingDef, bool singleStack = true)
        {
            Texture2D icon = ContentFinder<Texture2D>.Get(thingDef.graphicData.texPath, false);
            if (icon == null)
            {
                // Use the first texture in the folder
                icon = singleStack ? ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).FirstOrDefault() : ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).LastOrDefault();
                if (icon == null)
                {
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport", true);
                    Log.Warning("Universal Fermenter:: No texture at " + thingDef.graphicData.texPath + ".");
                }
            }
            return icon;
        }
    }
}
