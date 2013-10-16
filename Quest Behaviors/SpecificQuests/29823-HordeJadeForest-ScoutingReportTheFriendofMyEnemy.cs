// Behavior originally contributed by mastahg.
//
// DOCUMENTATION:
//     
//

using System;
using System.Collections.Generic;
using System.Linq;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.ScoutingReportTheFriendofMyEnemy
{
    [CustomBehaviorFileName(@"SpecificQuests\29823-HordeJadeForest-ScoutingReportTheFriendofMyEnemy")]
    public class TheFriendofMyEnemy : CustomForcedBehavior
    {
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        public TheFriendofMyEnemy(Dictionary<string, string> args) : base(args)
        {
            try
            {
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                //Location = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ??WoWPoint.Empty;
                QuestId = 29823; //GetAttributeAsNullable<int>("QuestId", true, ConstrainAs.QuestId(this), null) ?? 0;
                //MobIds = GetAttributeAsNullable<int>("MobId", true, ConstrainAs.MobId, null) ?? 0;
                QuestRequirementComplete = QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = QuestInLogRequirement.InLog;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message + "\nFROM HERE:\n" + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public uint[] MobIds { get; private set; }
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public WoWPoint Location { get; private set; }

        // Private variables for internal state


        // Private properties
        private LocalPlayer Me
        {
            get { return (StyxWoW.Me); }
        }

        ~TheFriendofMyEnemy()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    CharacterSettings.Instance.UseMount = _useMount;
                    TreeHooks.Instance.RemoveHook("Combat_Main", CreateBehavior_MainCombat());
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        #region Overrides of CustomForcedBehavior

        private bool _useMount;
        private bool spoke = false;
        private WoWPoint spot = new WoWPoint(370.5139, -2026.915, 57.19295);

        public Composite DoneYet
        {
            get
            {
                return new Decorator(
                    ret => IsQuestComplete(),
                    new Action(
                        delegate
                        {
                            TreeRoot.StatusText = "Finished!";
                            CharacterSettings.Instance.UseMount = true;
                            _isBehaviorDone = true;
                            return RunStatus.Success;
                        }));
            }
        }


        public WoWUnit Kiryn
        {
            get { return ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(r => r.Entry == 55688); }
        }


        //<Vendor Name="Shademaster Kiryn" Entry="55688" Type="Repair" X="370.5139" Y="-2026.915" Z="57.19295" />
        public WoWUnit Enemy
        {
            get
            {
                var charmedUnit = Me.CharmedUnit;
                var kiryn = Kiryn;
                if (charmedUnit == null)
                    return null;
                return (from unit in ObjectManager.GetObjectsOfTypeFast<WoWUnit>()
                    let target = unit.CurrentTarget
                    let orderByLoc = kiryn != null ? kiryn.Location : charmedUnit.Location
                    where target != null && (target == kiryn || target == charmedUnit)
                    orderby orderByLoc.DistanceSqr(unit.Location)
                    select unit).FirstOrDefault();
            }
        }


        public Composite PhaseOne
        {
            get
            {
                return new Decorator(
                    r => !spoke,
                    new PrioritySelector(
                        new Decorator(r => Kiryn == null || !Kiryn.WithinInteractRange, new Action(r => Navigator.MoveTo(spot))),
                        new Decorator(r => !GossipFrame.Instance.IsVisible, new Action(r => Kiryn.Interact())),
                        new Decorator(
                            r => GossipFrame.Instance.IsVisible,
                            new Action(
                                r =>
                                {
                                    GossipFrame.Instance.SelectGossipOption(0);

                                    spoke = true;
                                }))));
            }
        }


        public Composite PhaseTwo
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(r => Me.CurrentTarget == null && Enemy != null, new Action(r => Enemy.Target())),
                    new Decorator(
                        r => Me.CurrentTarget != null,
                        new Action(
                            r =>
                            {
                                var target = Me.CurrentTarget;
                                //target.Face();
                                if (target.IsWithinMeleeRange)
                                {
                                    CastSpell("Uppercut");
                                }
                                else if (target.DistanceSqr > 5 * 5)
                                {
                                    CastSpell("Fling Filth");
                                }
                            })));
            }
        }

        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);
            return quest == null || quest.IsCompleted;
        }

        public void CastSpell(string action)
        {
            var spell = StyxWoW.Me.PetSpells.FirstOrDefault(p => p.ToString() == action);
            if (spell == null)
                return;

            Logging.Write("[Pet] Casting {0}", action);
            Lua.DoString("CastPetAction({0})", spell.ActionBarIndex + 1);
        }


        protected Composite CreateBehavior_MainCombat()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, PhaseOne, PhaseTwo, new ActionAlwaysSucceed())));
        }


        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                _useMount = CharacterSettings.Instance.UseMount;
                CharacterSettings.Instance.UseMount = false;
                TreeHooks.Instance.InsertHook("Combat_Main", 0, CreateBehavior_MainCombat());
                // Me.QuestLog.GetQuestById(27761).GetObjectives()[2].

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint) QuestId);

                TreeRoot.GoalText = GetType().Name + ": " + ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress");
            }
        }

        #endregion
    }
}