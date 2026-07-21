using Orion.Api;
using Orion.Api.Blocks;
using Orion.Api.Items;
using Orion.Api.Math;
using Orion.Gameplay;
using Orion.PluginContracts.Services;

namespace OrionMining.Break;

/// <summary>
/// Break-time ticks for crack animation (Basalt subset: hardness + tool tags; no enchant/haste).
/// </summary>
internal static class BreakTime
{
    const float Tps = 20f;
    const float CompatibleToolMultiplier = 1.5f;
    const float IncompatibleToolMultiplier = 5.0f;
    const int MaxBreakTicks = 6000;

    enum ToolCategory
    {
        None,
        Axe,
        Hoe,
        Pickaxe,
        Shovel,
        Sword
    }

    public static int GetBreakTimeTicks(
        IPlayer player,
        BlockPos blockPosition,
        IServiceRegistry services)
    {
        IBlock? block = player.Dimension?.GetBlock(blockPosition.X, blockPosition.Y, blockPosition.Z);
        if (block is null)
        {
            return 20;
        }

        IBlockType blockType = block.Type;
        float hardness = blockType.Hardness;

        if (hardness < 0f)
        {
            return MaxBreakTicks;
        }

        // Minimal registries often leave Hardness at 0 for solids; use a diggable default.
        if (hardness == 0f)
        {
            if (blockType.Air || !blockType.Solid)
            {
                return 1;
            }

            hardness = 0.6f;
        }

        IItemStack? heldItem = ResolveHeldItem(player, services);

        ToolCategory requiredCategory = GetBlockToolCategory(blockType);
        int requiredTierLevel = GetBlockRequiredTierLevel(blockType);

        bool categoryMatch = false;
        int toolTierLevel = 0;

        if (heldItem is not null)
        {
            ToolCategory itemCategory = GetItemToolCategory(heldItem.Type);
            toolTierLevel = GetItemTierHarvestLevel(heldItem.Type);
            categoryMatch = requiredCategory != ToolCategory.None && itemCategory == requiredCategory;
        }

        bool tierOk = requiredTierLevel == 0 || toolTierLevel >= requiredTierLevel;
        bool compatible = requiredTierLevel == 0 || (categoryMatch && tierOk);

        float efficiency = 1f;
        if (categoryMatch && heldItem is not null)
        {
            efficiency = GetBaseMiningEfficiency(heldItem.Type);
        }

        float multiplier = compatible ? CompatibleToolMultiplier : IncompatibleToolMultiplier;
        float seconds = (hardness * multiplier) / efficiency;
        int ticks = (int)MathF.Ceiling(seconds * Tps);
        return Math.Clamp(ticks, 1, MaxBreakTicks);
    }

    /// <summary>Testable core: hardness + optional tool tags (empty = bare hand).</summary>
    internal static int ComputeTicks(float hardness, bool air, bool solid, IReadOnlyList<string> blockTags, IReadOnlyList<string>? itemTags)
    {
        if (hardness < 0f)
        {
            return MaxBreakTicks;
        }

        if (hardness == 0f)
        {
            if (air || !solid)
            {
                return 1;
            }

            hardness = 0.6f;
        }

        ToolCategory requiredCategory = GetBlockToolCategory(blockTags);
        int requiredTierLevel = GetBlockRequiredTierLevel(blockTags);

        bool categoryMatch = false;
        int toolTierLevel = 0;
        float efficiency = 1f;

        if (itemTags is { Count: > 0 })
        {
            ToolCategory itemCategory = GetItemToolCategory(itemTags);
            toolTierLevel = GetItemTierHarvestLevel(itemTags);
            categoryMatch = requiredCategory != ToolCategory.None && itemCategory == requiredCategory;
            if (categoryMatch)
            {
                efficiency = GetBaseMiningEfficiency(itemTags);
            }
        }

        bool tierOk = requiredTierLevel == 0 || toolTierLevel >= requiredTierLevel;
        bool compatible = requiredTierLevel == 0 || (categoryMatch && tierOk);
        float multiplier = compatible ? CompatibleToolMultiplier : IncompatibleToolMultiplier;
        float seconds = (hardness * multiplier) / efficiency;
        int ticks = (int)MathF.Ceiling(seconds * Tps);
        return Math.Clamp(ticks, 1, MaxBreakTicks);
    }

    static IItemStack? ResolveHeldItem(IPlayer player, IServiceRegistry services)
    {
        if (services.TryGet(out IPlayerInventoryService? inventoryService)
            && inventoryService is not null
            && inventoryService.TryGetAccess(player, out IPlayerInventoryAccess? access)
            && access is not null)
        {
            return access.GetHeldItem();
        }

        return null;
    }

    static ToolCategory GetBlockToolCategory(IBlockType blockType) => GetBlockToolCategory(blockType.Tags);

    static ToolCategory GetBlockToolCategory(IReadOnlyList<string> tags)
    {
        if (HasTag(tags, "minecraft:is_pickaxe_item_destructible")) return ToolCategory.Pickaxe;
        if (HasTag(tags, "minecraft:is_axe_item_destructible")) return ToolCategory.Axe;
        if (HasTag(tags, "minecraft:is_shovel_item_destructible")) return ToolCategory.Shovel;
        if (HasTag(tags, "minecraft:is_hoe_item_destructible")) return ToolCategory.Hoe;
        if (HasTag(tags, "minecraft:is_sword_item_destructible")) return ToolCategory.Sword;
        return ToolCategory.None;
    }

    static int GetBlockRequiredTierLevel(IBlockType blockType) => GetBlockRequiredTierLevel(blockType.Tags);

    static int GetBlockRequiredTierLevel(IReadOnlyList<string> tags)
    {
        if (HasTag(tags, "minecraft:diamond_tier_destructible")) return 5;
        if (HasTag(tags, "minecraft:iron_tier_destructible")) return 4;
        if (HasTag(tags, "minecraft:stone_tier_destructible")) return 3;
        return 0;
    }

    static bool HasTag(IReadOnlyList<string> tags, string tag)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i] == tag)
            {
                return true;
            }
        }

        return false;
    }

    static ToolCategory GetItemToolCategory(IItemType itemType) => GetItemToolCategory(itemType.Tags);

    static ToolCategory GetItemToolCategory(IReadOnlyList<string> tags)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            switch (tags[i])
            {
                case "minecraft:is_pickaxe": return ToolCategory.Pickaxe;
                case "minecraft:is_axe": return ToolCategory.Axe;
                case "minecraft:is_shovel": return ToolCategory.Shovel;
                case "minecraft:is_hoe": return ToolCategory.Hoe;
                case "minecraft:is_sword": return ToolCategory.Sword;
            }
        }

        return ToolCategory.None;
    }

    static int GetItemTierHarvestLevel(IItemType itemType) => GetItemTierHarvestLevel(itemType.Tags);

    static int GetItemTierHarvestLevel(IReadOnlyList<string> tags)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            switch (tags[i])
            {
                case "minecraft:netherite_tier": return 6;
                case "minecraft:diamond_tier": return 5;
                case "minecraft:iron_tier": return 4;
                case "minecraft:stone_tier": return 3;
                case "minecraft:copper_tier": return 3;
                case "minecraft:golden_tier": return 2;
                case "minecraft:wooden_tier": return 1;
            }
        }

        return 0;
    }

    static float GetBaseMiningEfficiency(IItemType itemType) => GetBaseMiningEfficiency(itemType.Tags);

    static float GetBaseMiningEfficiency(IReadOnlyList<string> tags)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            switch (tags[i])
            {
                case "minecraft:netherite_tier": return 9f;
                case "minecraft:diamond_tier": return 8f;
                case "minecraft:iron_tier": return 6f;
                case "minecraft:copper_tier": return 5f;
                case "minecraft:stone_tier": return 4f;
                case "minecraft:golden_tier": return 12f;
                case "minecraft:wooden_tier": return 2f;
            }
        }

        return 1f;
    }
}
