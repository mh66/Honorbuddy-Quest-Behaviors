﻿using Styx;
using Styx.Combat.CombatRoutine;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace Hera.Helpers
{
    public static class CLC
    {
        //*********************************************************************************************************
        // 
        // This is what I call my "Common Language Configuration" system.
        // It takes common language terms and makes a TRUE/FALSE setting from it.
        // I'm sure there are more elegant ways of undertaking this but it suites my purposes perfectly.
        //
        // You pass a RAW line of string and simply check if a phrase or keyword is present
        // Its a lot more intuitive for the user and it gives me more control over the UI
        // This way I don't need hundreds of tick boxes or controls
        // 
        //*********************************************************************************************************
       

        /// <summary>
        /// This is the property you assign the raw 'setting' to. The raw setting is the value passed from the Settings.XXXX property
        /// Eg CLC.RawSetting = Settings.Cleanse; Checking CLC.AfterCombatEnds you will see it returns TRUE
        /// </summary>
        public static string RawSetting { get; set; }
        private static LocalPlayer Me { get { return ObjectManager.Me; } }

        // Call these properties from the CC in order to check if a condition is met. 
        public static bool OnAdds { get { return RawSetting.Contains("only on adds"); } }
        public static bool NoAdds { get { return RawSetting.Contains("only when no adds"); } }
        public static bool Always { get { return RawSetting.Contains("always"); } }
        public static bool Never { get { return RawSetting.Contains("never"); } }
        public static bool OnRunners { get { return RawSetting.Contains("on runners") || RawSetting.Contains("and runners"); } }
        public static bool IfCasting { get { return RawSetting.Contains("on casters") || RawSetting.Contains("casters and"); } }
        public static bool IfCastingOrRunning { get { return RawSetting.Contains("casters and runners"); } }
        //public static bool OutOfCombat { get { return RawSetting.Contains("out of combat"); } }
        public static bool OutOfCombat { get { return RawSetting.Contains("out of combat") || RawSetting.Contains("during pull"); } }
        public static bool Immediately { get { return RawSetting.Contains("immediately"); } }
        public static bool InBGs { get { return RawSetting.Contains("only in battlegrounds"); } }
        public static bool InInstances { get { return RawSetting.Contains("only in instances"); } }

        // Combo points - Rogues and Druids (Cat)
        public static bool ComboPoints12 { get { return RawSetting.Contains("1-2 combo"); } }
        public static bool ComboPoints23 { get { return RawSetting.Contains("2-3 combo"); } }
        public static bool ComboPoints34 { get { return RawSetting.Contains("3-4 combo"); } }
        public static bool ComboPoints45 { get { return RawSetting.Contains("4-5 combo"); } }

        // Holy Power - Paladin only
        public static bool HolyPower1OrMore { get { return RawSetting.Contains("1+ Holy Power"); } }
        public static bool HolyPower2OrMore { get { return RawSetting.Contains("2+ Holy Power"); } }
        public static bool HolyPower3OrMore { get { return RawSetting.Contains("3+ Holy Power"); } }
        
        public static bool IsOkToRun
        {
            get
            {
                if (string.IsNullOrEmpty(RawSetting)) return false;                                                 // No string passed so nothing to check
                if (Always || Immediately) return true;                                                             // Always means always
                if (Never) return false;                                                                            // No means no! You men are all the same

                if (OutOfCombat && !Me.Combat) return true;                                                         // Only if we're not in combat
                if (OnAdds && Utils.Adds && Me.Combat) return true;                                                               // Only if we have adds
                if (NoAdds && !Utils.Adds && Me.Combat) return true;                                                               // Only if we DON'T have adds
                if (Me.GotTarget && IfCastingOrRunning && (Me.CurrentTarget.IsCasting || Me.CurrentTarget.Fleeing)) return true;    // Only if the target is casting or running
                if (OnRunners && Me.GotTarget && Me.CurrentTarget.Fleeing) return true;                                              // Only if target (NPC) is running away
                if (IfCasting && Me.GotTarget && Me.CurrentTarget.IsCasting) return true;                                            // Only if target is casting
                if (InInstances && Me.IsInInstance) return true;                                                    // Only if you are inside an instance
                if (InBGs && Utils.IsBattleground) return true;                                                     // Only if you are inside a battleground

                // If you are not a Rogue or a Druid (Cat) then don't do these checks
                if (Me.Class == WoWClass.Rogue || (Me.Class == WoWClass.Druid && Me.Shapeshift == ShapeshiftForm.Cat))
                {
                    if (Me.ComboPoints <= 0) return false;
                    if (ComboPoints45 && Me.ComboPoints >= 4) return true;
                    if (ComboPoints34 && (Me.ComboPoints >= 3 && Me.ComboPoints <= 4)) return true;
                    if (ComboPoints23 && (Me.ComboPoints >= 2 && Me.ComboPoints <= 3)) return true;
                    if (ComboPoints12 && (Me.ComboPoints >= 1 && Me.ComboPoints <= 2)) return true;
                }

                if (Me.Class == WoWClass.Paladin)
                {
                    //if (Me.CurrentHolyPower <= 0) return false;
                    if (HolyPower1OrMore && Me.CurrentHolyPower >= 1) return true;
                    if (HolyPower2OrMore && Me.CurrentHolyPower >= 2) return true;
                    if (HolyPower3OrMore && Me.CurrentHolyPower >= 3) return true;

                    // Other Misc
                    if (RawSetting.Contains("Sacred Duty") && IsBuffPresent("Sacred Duty")) return true;
                }

                return false;                                                                                       // Otherwise its not going to happen
            }

        }

        public static bool ResultOK(string clcSettingString)
        {
            CLC.RawSetting = clcSettingString;
            bool result = IsOkToRun;
            return result;
        }


        private static bool IsBuffPresent(string buffToCheck)
        {
            Lua.DoString("buffName,_,_,stackCount,_,_,_,_,_=UnitBuff(\"player\",\"" + buffToCheck + "\")");
            string buffName = Lua.GetLocalizedText("buffName", Me.BaseAddress);
            return (buffName == buffToCheck);
        }
    }
}