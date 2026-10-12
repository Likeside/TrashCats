using TrashCats.Domain;

namespace TrashCats.App
{
    /// <summary>Why a player command was rejected. Presentation maps each value to a localized message.</summary>
    public enum CommandError
    {
        None = 0,
        UnknownCat,
        UnknownActivity,
        InBattle,
        Tired,
        EnergyFull,
        StationBusy,
        AttributeMaxed,
        NotWorking,
        NoFood,
        AlreadyFull,
        InvalidSlot,
        UnknownSkill,
        SkillLocked,
        NotEquippable,
        AlreadyEquipped,
        UnknownUpgrade,
        MaxLevel,
        NotEnoughJunk,
    }

    public readonly struct CommandResult
    {
        public static readonly CommandResult Ok = default;

        public CommandError Error { get; }
        public bool IsOk => Error == CommandError.None;

        private CommandResult(CommandError error) => Error = error;

        public static CommandResult Fail(CommandError error) => new CommandResult(error);

        public override string ToString() => IsOk ? "Ok" : $"Fail({Error})";
    }

    internal static class CommandErrors
    {
        public static CommandError From(ActivityError error) => error switch
        {
            ActivityError.None => CommandError.None,
            ActivityError.UnknownActivity => CommandError.UnknownActivity,
            ActivityError.InBattle => CommandError.InBattle,
            ActivityError.Tired => CommandError.Tired,
            ActivityError.EnergyFull => CommandError.EnergyFull,
            ActivityError.StationBusy => CommandError.StationBusy,
            _ => CommandError.AttributeMaxed,
        };

        public static CommandError From(FeedError error) => error switch
        {
            FeedError.None => CommandError.None,
            FeedError.InBattle => CommandError.InBattle,
            FeedError.NoFood => CommandError.NoFood,
            _ => CommandError.AlreadyFull,
        };

        public static CommandError From(SkillEquipError error) => error switch
        {
            SkillEquipError.None => CommandError.None,
            SkillEquipError.InvalidSlot => CommandError.InvalidSlot,
            SkillEquipError.UnknownSkill => CommandError.UnknownSkill,
            SkillEquipError.SkillLocked => CommandError.SkillLocked,
            SkillEquipError.NotEquippable => CommandError.NotEquippable,
            _ => CommandError.AlreadyEquipped,
        };

        public static CommandError From(UpgradeError error) => error switch
        {
            UpgradeError.None => CommandError.None,
            UpgradeError.UnknownUpgrade => CommandError.UnknownUpgrade,
            UpgradeError.MaxLevel => CommandError.MaxLevel,
            _ => CommandError.NotEnoughJunk,
        };
    }
}
