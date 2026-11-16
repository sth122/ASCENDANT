using System.Collections.Generic;

public readonly struct CreditEntry
{
    public readonly string Role;
    public readonly string Name;

    public CreditEntry(string role, string name)
    {
        Role = role;
        Name = name;
    }
}

public readonly struct AssetCreditItem
{
    public readonly string AssetTitle;
    public readonly string SourceUrl;
    public readonly string ProviderNote;

    public AssetCreditItem(string assetTitle, string sourceUrl, string providerNote = "")
    {
        AssetTitle = assetTitle;
        SourceUrl = sourceUrl;
        ProviderNote = providerNote;
    }
}

public class AssetCreditCategory
{
    public string CategoryTitle { get; }
    public List<AssetCreditItem> Items { get; } = new List<AssetCreditItem>();

    public AssetCreditCategory(string categoryTitle)
    {
        CategoryTitle = categoryTitle;
    }

    public AssetCreditCategory Add(string assetTitle, string sourceUrl, string providerNote = "")
    {
        Items.Add(new AssetCreditItem(assetTitle, sourceUrl, providerNote));
        return this;
    }
}

public class EndingCreditsModel : IUIModel
{
    public string GameTitle { get; set; } = "ASCENDANT";
    public string Subtitle { get; set; } = "Thank You For Playing";
    public float BaseScrollSpeed { get; set; } = 75f;
    public float FastForwardMultiplier { get; set; } = 3.0f;

    public List<CreditEntry> Entries { get; } = new List<CreditEntry>();
    public List<AssetCreditCategory> AssetCategories { get; } = new List<AssetCreditCategory>();

    public static EndingCreditsModel CreateDefault()
    {
        EndingCreditsModel model = new EndingCreditsModel();

        // 1. 개발진 크레딧
        model.Entries.Add(new CreditEntry("GAME DESIGN & PROGRAMMING", "송태훈"));
        model.Entries.Add(new CreditEntry("COMBAT & AI ARCHITECTURE", "송태훈"));
        model.Entries.Add(new CreditEntry("UI / UX & SYSTEM PIPELINE", "송태훈"));
        model.Entries.Add(new CreditEntry("ENGINE", "Unity 6.3 LTS"));

        // 2. 에셋 카테고리별 저작권 및 출처 표기
        model.AssetCategories.Add(
            new AssetCreditCategory("3D MODELS & CHARACTERS")
                .Add("Bonfire Model", "https://sketchfab.com/3d-models/bonfire-6f61e3ee52594ba0a416b2eb30d40efe")
                .Add("Tarisland Dragon High Poly", "https://sketchfab.com/3d-models/tarisland-dragon-high-poly-ecf63885166c40e2bbbcdf11cd14e65f")
                .Add("Character Models & Animations", "Adobe Mixamo")
                .Add("Nightmare Creature 1", "https://sketchfab.com/3d-models/nightmare-creature-1-9fb509d207d744babeb5d87a486f7de1")
        );

        model.AssetCategories.Add(
            new AssetCreditCategory("SOUND & MUSIC")
                .Add("Footsteps Sounds Volume 02", "https://assetstore.unity.com/packages/audio/sound-fx/footsteps-sounds-volume-02-351282")
                .Add("Fantasy Boss Battle Music Pack Vol.2", "https://alkakrab.itch.io/fantasy-boss-battle-music-pack-vol-2")
                .Add("6 Dark Fantasy Boss Battle Tracks", "https://alkakrab.itch.io/6-dark-fantasy-boss-battle-tracks")
                .Add("Free Sound Effects Pack - Starter All Categories", "https://assetstore.unity.com/packages/audio/sound-fx/free-sound-effects-pack-starter-all-categories-155776")
        );

        model.AssetCategories.Add(
            new AssetCreditCategory("VFX & PARTICLES")
                .Add("Free Fire VFX", "https://assetstore.unity.com/packages/vfx/particles/fire-explosions/free-fire-vfx-266227")
                .Add("Magic Effects Free", "https://assetstore.unity.com/packages/vfx/particles/spells/magic-effects-free-247933")
                .Add("Particle Pack - Starter Assets", "https://assetstore.unity.com/packages/vfx/particles/particle-pack-starter-assets-127325")
        );

        model.AssetCategories.Add(
            new AssetCreditCategory("ENVIRONMENT")
                .Add("Spaceskies Free", "https://assetstore.unity.com/packages/2d/textures-materials/sky/spaceskies-free-80503")
                .Add("Dark Dungeon Modular", "https://assetstore.unity.com/packages/3d/environments/dungeons/dark-dungeon-modular-325055", "넥스트러너스(캠프) 제공")
        );

        model.AssetCategories.Add(
            new AssetCreditCategory("GUI")
                .Add("4K Full Fantasy GUI - Over 400 PNG Samples", "https://assetstore.unity.com/packages/2d/gui/4k-full-fantasy-gui-over-400-png-samples-116891", "넥스트러너스(캠프) 제공")
        );

        return model;
    }

    public void Reset()
    {
        Entries.Clear();
        AssetCategories.Clear();
    }
}