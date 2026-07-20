using Orion.Gameplay;
using Orion.Item;
using Orion.Plugins;
using Orion.Protocol.Types;
using Orion.World;

namespace OrionMining;

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

    public static int GetBreakTimeTicks(global::Orion.Player.Player player, BlockPos blockPosition)
    {
        Orion.Block.BlockPermutation? block =
            player.Dimension?.GetGameplayPermutation(blockPosition.X, blockPosition.Y, blockPosition.Z);

        if (block is null)
        {
            return 20;
        }

        Orion.Block.BlockType blockType = block.Type;
        float hardness = blockType.Hardness;

        if (hardness < 0f)
        {
            return MaxBreakTicks;
        }

        // Orion's minimal registry historically left Hardness at 0 for solids; treat that as a
        // diggable default so crack animation / multiplayer sync have a real duration.
        if (hardness == 0f)
        {
            if (blockType.Air || !blockType.Solid)
            {
                return 1;
            }

            hardness = 0.6f;
        }

        ItemStack? heldItem = null;
        if (PluginHost.Services.TryGet(out IPlayerInventoryService? inventoryService)
            && inventoryService is not null
            && inventoryService.TryGetAccess(player, out IPlayerInventoryAccess? access)
            && access is not null)
        {
            heldItem = access.GetHeldItem();
        }

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

    static ToolCategory GetBlockToolCategory(Orion.Block.BlockType blockType)
    {
        if (HasTag(blockType, "minecraft:is_pickaxe_item_destructible")) return ToolCategory.Pickaxe;
        if (HasTag(blockType, "minecraft:is_axe_item_destructible")) return ToolCategory.Axe;
        if (HasTag(blockType, "minecraft:is_shovel_item_destructible")) return ToolCategory.Shovel;
        if (HasTag(blockType, "minecraft:is_hoe_item_destructible")) return ToolCategory.Hoe;
        if (HasTag(blockType, "minecraft:is_sword_item_destructible")) return ToolCategory.Sword;
        return ToolCategory.None;
    }

    static int GetBlockRequiredTierLevel(Orion.Block.BlockType blockType)
    {
        if (HasTag(blockType, "minecraft:diamond_tier_destructible")) return 5;
        if (HasTag(blockType, "minecraft:iron_tier_destructible")) return 4;
        if (HasTag(blockType, "minecraft:stone_tier_destructible")) return 3;
        return 0;
    }

    static bool HasTag(Orion.Block.BlockType blockType, string tag)
        => blockType.Tags.Contains(tag);

    static ToolCategory GetItemToolCategory(ItemType itemType)
    {
        IReadOnlyList<string> tags = itemType.Tags;
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

    static int GetItemTierHarvestLevel(ItemType itemType)
    {
        IReadOnlyList<string> tags = itemType.Tags;
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

    static float GetBaseMiningEfficiency(ItemType itemType)
    {
        IReadOnlyList<string> tags = itemType.Tags;
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
