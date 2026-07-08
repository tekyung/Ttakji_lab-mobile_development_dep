namespace TCG_Project.Scripts.Core
{
    public enum CharacterSlotType
    {
        Main,
        Sub
    }

    public readonly struct CharacterSlotSnapshot
    {
        public Player Owner { get; }
        public CharacterSlotType Slot { get; }
        public string CharacterCardId { get; }
        public string ImageResourcesPath { get; }
        public float RotationZ { get; }
        public bool AbilityUsed { get; }

        public CharacterSlotSnapshot(
            Player owner,
            CharacterSlotType slot,
            string characterCardId,
            string imageResourcesPath,
            float rotationZ,
            bool abilityUsed)
        {
            Owner = owner;
            Slot = slot;
            CharacterCardId = characterCardId;
            ImageResourcesPath = imageResourcesPath;
            RotationZ = rotationZ;
            AbilityUsed = abilityUsed;
        }
    }
}
