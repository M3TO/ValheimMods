﻿using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using EpicLoot.GatedItemType;
using EpicLoot.LegendarySystem;
using ExtendedItemDataFramework;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace EpicLoot
{
    public static class LootRoller
    {
        public static LootConfig Config;
        public static readonly Dictionary<string, LootItemSet> ItemSets = new Dictionary<string, LootItemSet>();
        public static readonly Dictionary<string, List<LootTable>> LootTables = new Dictionary<string, List<LootTable>>();

        public static event Action<ExtendedItemData, MagicItem> MagicItemGenerated;

        private static WeightedRandomCollection<int[]> _weightedDropCountTable;
        private static WeightedRandomCollection<LootDrop> _weightedLootTable;
        private static WeightedRandomCollection<MagicItemEffectDefinition> _weightedEffectTable;
        private static WeightedRandomCollection<KeyValuePair<int, int>> _weightedEffectCountTable;
        private static WeightedRandomCollection<KeyValuePair<ItemRarity, int>> _weightedRarityTable;
        private static WeightedRandomCollection<LegendaryInfo> _weightedLegendaryTable;
        public static int CheatEffectCount;
        public static bool CheatDisableGating;
        public static bool CheatForceMagicEffect;
        public static string ForcedMagicEffect = "";
        public static string CheatForceLegendary;

        public static void Initialize(LootConfig lootConfig)
        {
            Config = lootConfig;
            
            var random = new System.Random();
            _weightedDropCountTable = new WeightedRandomCollection<int[]>(random);
            _weightedLootTable = new WeightedRandomCollection<LootDrop>(random);
            _weightedEffectTable = new WeightedRandomCollection<MagicItemEffectDefinition>(random);
            _weightedEffectCountTable = new WeightedRandomCollection<KeyValuePair<int, int>>(random);
            _weightedRarityTable = new WeightedRandomCollection<KeyValuePair<ItemRarity, int>>(random);
            _weightedLegendaryTable = new WeightedRandomCollection<LegendaryInfo>(random);

            ItemSets.Clear();
            LootTables.Clear();
            if (Config == null)
            {
                EpicLoot.LogWarning("Initialized LootRoller with null");
                return;
            }
          
            AddItemSets(lootConfig.ItemSets);
            AddLootTables(lootConfig.LootTables);
        }

        private static void AddItemSets([NotNull] IEnumerable<LootItemSet> itemSets)
        {
            foreach (var itemSet in itemSets)
            {
                if (string.IsNullOrEmpty(itemSet.Name))
                {
                    EpicLoot.LogError($"Tried to add ItemSet with no name!");
                    continue;
                }

                if (!ItemSets.ContainsKey(itemSet.Name))
                {
                    EpicLoot.Log($"Added ItemSet: {itemSet.Name}");
                    ItemSets.Add(itemSet.Name, itemSet);
                }
                else
                {
                    EpicLoot.LogError($"Tried to add ItemSet {itemSet.Name}, but it already exists!");
                }
            }
        }

        public static void AddLootTables([NotNull] IEnumerable<LootTable> lootTables)
        {

            // Add loottables for mobs or objects that do not reference another template
            foreach (var lootTable in lootTables.Where(x => x.RefObject == null || x.RefObject == ""))
            {
                AddLootTable(lootTable);
                EpicLoot.Log($"Added loottable for {lootTable.Object}");
            }

            // Add loottables that are based off mob or object templates
            foreach (var lootTable in lootTables.Where(x => x.RefObject != null && x.RefObject != ""))
            {
                AddLootTable(lootTable);
                EpicLoot.Log($"Added loottable for {lootTable.Object} using {lootTable.RefObject} as reference");
            }
        }

        public static void AddLootTable([NotNull] LootTable lootTable)
        {
            var key = lootTable.Object;
            if (string.IsNullOrEmpty(key))
            {
                EpicLoot.LogError("Loot table missing Object name!");
                return;
            }

            EpicLoot.Log($"Added LootTable: {key}");
            if (!LootTables.ContainsKey(key))
            {
                LootTables.Add(key, new List<LootTable>());
            }

            var refKey = lootTable.RefObject;
            if (string.IsNullOrEmpty(refKey))
            {
                LootTables[key].Add(lootTable);

            }
            else
            {
                if (!LootTables.ContainsKey(refKey))
                {
                    EpicLoot.LogError("Loot table missing RefObject name!");
                    return;
                }
                else
                {
                    LootTables[key] = LootTables[refKey];
                }
            }
        }

        public static List<GameObject> RollLootTableAndSpawnObjects(List<LootTable> lootTables, int level, string objectName, Vector3 dropPoint)
        {
            return RollLootTableInternal(lootTables, level, objectName, dropPoint, true);
        }

        public static List<GameObject> RollLootTableAndSpawnObjects(LootTable lootTable, int level, string objectName, Vector3 dropPoint)
        {
            return RollLootTableInternal(lootTable, level, objectName, dropPoint, true);
        }

        public static List<ItemDrop.ItemData> RollLootTable(List<LootTable> lootTables, int level, string objectName, Vector3 dropPoint)
        {
            var results = new List<ItemDrop.ItemData>();
            var gameObjects = RollLootTableInternal(lootTables, level, objectName, dropPoint, false);
            foreach (var itemObject in gameObjects)
            {
                results.Add(itemObject.GetComponent<ItemDrop>().m_itemData.Clone());
                Object.Destroy(itemObject);
            }

            return results;
        }

        public static List<ItemDrop.ItemData> RollLootTable(LootTable lootTable, int level, string objectName, Vector3 dropPoint)
        {
            return RollLootTable(new List<LootTable> {lootTable}, level, objectName, dropPoint);
        }

        public static List<ItemDrop.ItemData> RollLootTable(string lootTableName, int level, string objectName, Vector3 dropPoint)
        {
            var lootTable = GetLootTable(lootTableName);
            if (lootTable == null)
            {
                return new List<ItemDrop.ItemData>();
            }

            return RollLootTable(lootTable, level, objectName, dropPoint);
        }

        private static List<GameObject> RollLootTableInternal(IEnumerable<LootTable> lootTables, int level, string objectName, Vector3 dropPoint, bool initializeObject)
        {
            var results = new List<GameObject>();
            foreach (var lootTable in lootTables)
            {
                results.AddRange(RollLootTableInternal(lootTable, level, objectName, dropPoint, initializeObject));
            }
            return results;
        }

        private static List<GameObject> RollLootTableInternal(LootTable lootTable, int level, string objectName, Vector3 dropPoint, bool initializeObject)
        {
            var results = new List<GameObject>();
            if (lootTable == null || level <= 0 || string.IsNullOrEmpty(objectName))
            {
                return results;
            }

            var drops = GetDropsForLevel(lootTable, level);
            if (ArrayUtils.IsNullOrEmpty(drops))
            {
                return results;
            }

            if (EpicLoot.AlwaysDropCheat)
            {
                drops = drops.Where(x => x.Length > 0 && x[0] != 0).ToArray();
            }

            _weightedDropCountTable.Setup(drops, dropPair => dropPair.Length == 2 ? dropPair[1] : 1);
            var dropCountRollResult = _weightedDropCountTable.Roll();
            var dropCount = dropCountRollResult != null && dropCountRollResult.Length >= 1 ? dropCountRollResult[0] : 0;
            if (dropCount == 0)
            {
                return results;
            }

            var loot = GetLootForLevel(lootTable, level);
            _weightedLootTable.Setup(loot, x => x.Weight);
            var selectedDrops = _weightedLootTable.Roll(dropCount);

            foreach (var ld in selectedDrops)
            {
                if (ld == null)
                {
                    EpicLoot.LogError($"Loot drop was null! RollLootTableInternal({lootTable.Object}, {level}, {objectName})");
                    continue;
                }
                var lootDrop = ResolveLootDrop(ld, ld.Rarity);
                var itemID = CheatDisableGating ? lootDrop.Item : GatedItemTypeHelper.GetGatedItemID(lootDrop.Item);

                var itemPrefab = ObjectDB.instance.GetItemPrefab(itemID);
                if (itemPrefab == null)
                {
                    EpicLoot.LogError($"Tried to spawn loot ({itemID}) for ({objectName}), but the item prefab was not found!");
                    continue;
                }

                var randomRotation = Quaternion.Euler(0.0f, Random.Range(0.0f, 360.0f), 0.0f);
                ZNetView.m_forceDisableInit = !initializeObject;
                var item = Object.Instantiate(itemPrefab, dropPoint, randomRotation);
                ZNetView.m_forceDisableInit = false;
                var itemDrop = item.GetComponent<ItemDrop>();
                if (EpicLoot.CanBeMagicItem(itemDrop.m_itemData) && !ArrayUtils.IsNullOrEmpty(lootDrop.Rarity))
                {
                    var itemData = new ExtendedItemData(itemDrop.m_itemData);
                    var magicItemComponent = itemData.AddComponent<MagicItemComponent>();
                    var magicItem = RollMagicItem(lootDrop, itemData);
                    if (CheatForceMagicEffect)
                    {
                        AddDebugMagicEffects(magicItem);
                    }

                    magicItemComponent.SetMagicItem(magicItem);

                    itemDrop.m_itemData = itemData;
                    InitializeMagicItem(itemData);

                    MagicItemGenerated?.Invoke(itemData, magicItem);
                }

                results.Add(item);
            }

            return results;
        }

        private static LootDrop ResolveLootDrop(LootDrop lootDrop, int[] rarityOverride)
        {
            var result = new LootDrop { Item = lootDrop.Item, Rarity = lootDrop.Rarity, Weight = lootDrop.Weight };
            var needsResolve = true;
            while (needsResolve)
            {
                if (ItemSets.TryGetValue(result.Item, out var itemSet))
                {
                    if (itemSet.Loot.Length == 0)
                    {
                        EpicLoot.LogError($"Tried to roll using ItemSet ({itemSet.Name}) but its loot list was empty!");
                    }
                    _weightedLootTable.Setup(itemSet.Loot, x => x.Weight);
                    result = _weightedLootTable.Roll();
                    if (!ArrayUtils.IsNullOrEmpty(rarityOverride))
                    {
                        result.Rarity = rarityOverride;
                    }
                }
                else if (IsLootTableRefence(result.Item, out var lootList))
                {
                    if (lootList.Length == 0)
                    {
                        EpicLoot.LogError($"Tried to roll using loot table reference ({result.Item}) but its loot list was empty!");
                    }
                    _weightedLootTable.Setup(lootList, x => x.Weight);
                    result = _weightedLootTable.Roll();
                    if (ArrayUtils.IsNullOrEmpty(rarityOverride))
                    {
                        result.Rarity = rarityOverride;
                    }
                }
                else
                {
                    needsResolve = false;
                }
            }

            return result;
        }

        private static bool IsLootTableRefence(string lootDropItem, out LootDrop[] lootList)
        {
            lootList = null;
            var parts = lootDropItem.Split('.');
            if (parts.Length != 2)
            {
                return false;
            }

            var objectName = parts[0];
            var levelText = parts[1];
            if (!int.TryParse(levelText, out var level))
            {
                EpicLoot.LogError($"Tried to get a loot table reference from '{lootDropItem}' but could not parse the level value ({levelText})!");
                return false;
            }

            if (LootTables.ContainsKey(objectName))
            {
                var lootTable = LootTables[objectName].FirstOrDefault();
                if (lootTable != null)
                {
                    lootList = GetLootForLevel(lootTable, level);
                    return true;
                }

                EpicLoot.LogError($"UNLIKELY: LootTables contains entry for {objectName} but no valid loot tables! Weird!");
            }

            return false;
        }

        public static MagicItem RollMagicItem(LootDrop lootDrop, ExtendedItemData baseItem)
        {
            var rarity = RollItemRarity(lootDrop);
            return RollMagicItem(rarity, baseItem);
        }

        public static MagicItem RollMagicItem(ItemRarity rarity, ExtendedItemData baseItem)
        {
            var cheatLegendary = !string.IsNullOrEmpty(CheatForceLegendary);
            if (cheatLegendary)
            {
                rarity = ItemRarity.Legendary;
            }

            var magicItem = new MagicItem { Rarity = rarity };

            var effectCount = CheatEffectCount >= 1 ? CheatEffectCount : RollEffectCountPerRarity(magicItem.Rarity);

            if (rarity == ItemRarity.Legendary)
            {
                LegendaryInfo legendary = null;
                if (cheatLegendary)
                {
                    UniqueLegendaryHelper.TryGetLegendaryInfo(CheatForceLegendary, out legendary);
                }
                
                if (legendary == null)
                {
                    var availableLegendaries = UniqueLegendaryHelper.GetAvailableLegendaries(baseItem, magicItem);
                    _weightedLegendaryTable.Setup(availableLegendaries, x => x.SelectionWeight);
                    legendary = _weightedLegendaryTable.Roll();
                }

                if (!UniqueLegendaryHelper.IsGenericLegendary(legendary))
                {
                    magicItem.LegendaryID = legendary.ID;
                    magicItem.DisplayName = legendary.Name;

                    foreach (var guaranteedMagicEffect in legendary.GuaranteedMagicEffects)
                    {
                        var effectDef = MagicItemEffectDefinitions.Get(guaranteedMagicEffect.Type);
                        if (effectDef == null)
                        {
                            EpicLoot.LogError($"Could not find magic effect (Type={guaranteedMagicEffect.Type}) while creating legendary item (ID={legendary.ID})");
                            continue;
                        }

                        var effect = RollEffect(effectDef, ItemRarity.Legendary, guaranteedMagicEffect.Values);
                        magicItem.Effects.Add(effect);
                        effectCount--;
                    }
                }
            }

            for (var i = 0; i < effectCount; i++)
            {
                var availableEffects = MagicItemEffectDefinitions.GetAvailableEffects(baseItem, magicItem);
                if (availableEffects.Count == 0)
                {
                    EpicLoot.LogWarning($"Tried to add more effects to magic item ({baseItem.m_shared.m_name}) but there were no more available effects. " +
                                        $"Current Effects: {(string.Join(", ", magicItem.Effects.Select(x => x.EffectType.ToString())))}");
                    break;
                }

                _weightedEffectTable.Setup(availableEffects, x => x.SelectionWeight);
                var effectDef = _weightedEffectTable.Roll();

                var effect = RollEffect(effectDef, magicItem.Rarity);
                magicItem.Effects.Add(effect);
            }

            if (string.IsNullOrEmpty(magicItem.DisplayName))
            {
                magicItem.DisplayName = MagicItemNames.GetNameForItem(baseItem, magicItem);
            }

            return magicItem;
        }

        private static void InitializeMagicItem(ExtendedItemData baseItem)
        {
            if (baseItem.m_shared.m_useDurability)
            {
                baseItem.m_durability = Random.Range(0.2f, 1.0f) * baseItem.GetMaxDurability();
            }
        }

        public static int RollEffectCountPerRarity(ItemRarity rarity)
        {
            var countPercents = GetEffectCountsPerRarity(rarity);
            _weightedEffectCountTable.Setup(countPercents, x => x.Value);
            return _weightedEffectCountTable.Roll().Key;
        }

        public static List<KeyValuePair<int, int>> GetEffectCountsPerRarity(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Magic:
                    return Config.MagicEffectsCount.Magic.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
                case ItemRarity.Rare:
                    return Config.MagicEffectsCount.Rare.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
                case ItemRarity.Epic:
                    return Config.MagicEffectsCount.Epic.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
                case ItemRarity.Legendary:
                    return Config.MagicEffectsCount.Legendary.Select(x => new KeyValuePair<int, int>(x[0], x[1])).ToList();
                default:
                    throw new ArgumentOutOfRangeException(nameof(rarity), rarity, null);
            }
        }

        public static MagicItemEffect RollEffect(MagicItemEffectDefinition effectDef, ItemRarity itemRarity, MagicItemEffectDefinition.ValueDef valuesOverride = null)
        {
            float value = 0;
            var valuesDef = valuesOverride ?? effectDef.GetValuesForRarity(itemRarity);
            if (valuesDef != null)
            {
                value = valuesDef.MinValue;
                if (valuesDef.Increment != 0)
                {
                    EpicLoot.Log($"RollEffect: {effectDef.Type} {itemRarity} value={value} (min={valuesDef.MinValue} max={valuesDef.MaxValue})");
                    var incrementCount = (int)((valuesDef.MaxValue - valuesDef.MinValue) / valuesDef.Increment);
                    value = valuesDef.MinValue + (Random.Range(0, incrementCount + 1) * valuesDef.Increment);
                }
            }

            return new MagicItemEffect()
            {
                EffectType = effectDef.Type,
                EffectValue = value
            };
        }

        public static List<MagicItemEffect> RollEffects(List<MagicItemEffectDefinition> availableEffects, ItemRarity itemRarity, int count, bool removeOnSelect = true)
        {
            var results = new List<MagicItemEffect>();

            _weightedEffectTable.Setup(availableEffects, x => x.SelectionWeight, removeOnSelect);
            var effectDefs = _weightedEffectTable.Roll(count);

            foreach (var effectDef in effectDefs)
            {
                if (effectDef == null)
                {
                    EpicLoot.LogError($"EffectDef was null! RollEffects({itemRarity}, {count})");
                    continue;
                }
                results.Add(RollEffect(effectDef, itemRarity));
            }

            return results;
        }

        public static ItemRarity RollItemRarity(LootDrop lootDrop)
        {
            if (lootDrop.Rarity == null || lootDrop.Rarity.Length == 0)
            {
                return ItemRarity.Magic;
            }

            var rarityWeights = new Dictionary<ItemRarity, int>()
            {
                { ItemRarity.Magic, lootDrop.Rarity.Length >= 1 ? lootDrop.Rarity[0] : 0 },
                { ItemRarity.Rare, lootDrop.Rarity.Length >= 2 ? lootDrop.Rarity[1] : 0 },
                { ItemRarity.Epic, lootDrop.Rarity.Length >= 3 ? lootDrop.Rarity[2] : 0 },
                { ItemRarity.Legendary, lootDrop.Rarity.Length >= 4 ? lootDrop.Rarity[3] : 0 }
            };

            _weightedRarityTable.Setup(rarityWeights, x => x.Value);
            return _weightedRarityTable.Roll().Key;
        }

        public static List<LootTable> GetLootTable(string objectName)
        {
            var results = new List<LootTable>();
            if (LootTables.TryGetValue(objectName, out var lootTables))
            {
                foreach (var lootTable in lootTables)
                {
                    results.Add(lootTable);
                }
            }
            return results;
        }

        public static int[][] GetDropsForLevel([NotNull] LootTable lootTable, int level, bool useNextHighestIfNotPresent = true)
        {
            if (level == 3 && !ArrayUtils.IsNullOrEmpty(lootTable.Drops3))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    EpicLoot.LogWarning($"Duplicated leveled drops for ({lootTable.Object} lvl {level}), using 'Drops{level}'");
                }
                return lootTable.Drops3;
            }
            
            if ((level == 2 || level == 3) && !ArrayUtils.IsNullOrEmpty(lootTable.Drops2))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    EpicLoot.LogWarning($"Duplicated leveled drops for ({lootTable.Object} lvl {level}), using 'Drops{level}'");
                }
                return lootTable.Drops2;
            }

            if (level <= 3 && !ArrayUtils.IsNullOrEmpty(lootTable.Drops))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    EpicLoot.LogWarning($"Duplicated leveled drops for ({lootTable.Object} lvl {level}), using 'Drops'");
                }
                return lootTable.Drops;
            }

            for (var lvl = level; lvl >= 1; --lvl)
            {
                var found = lootTable.LeveledLoot.Find(x => x.Level == lvl);
                if (found != null && !ArrayUtils.IsNullOrEmpty(found.Drops))
                {
                    return found.Drops.ToArray();
                }

                if (!useNextHighestIfNotPresent)
                {
                    return null;
                }
            }

            EpicLoot.LogError($"Could not find any leveled drops for ({lootTable.Object} lvl {level}), but a loot table exists for this object!");
            return null;
        }

        public static LootDrop[] GetLootForLevel([NotNull] LootTable lootTable, int level, bool useNextHighestIfNotPresent = true)
        {
            if (level == 3 && !ArrayUtils.IsNullOrEmpty(lootTable.Loot3))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    EpicLoot.LogWarning($"Duplicated leveled loot for ({lootTable.Object} lvl {level}), using 'Loot{level}'");
                }
                return lootTable.Loot3.ToArray();
            }

            if ((level == 2 || level == 3) && !ArrayUtils.IsNullOrEmpty(lootTable.Loot2))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    EpicLoot.LogWarning($"Duplicated leveled loot for ({lootTable.Object} lvl {level}), using 'Loot{level}'");
                }
                return lootTable.Loot2.ToArray();
            }
            
            if (level <= 3 && !ArrayUtils.IsNullOrEmpty(lootTable.Loot))
            {
                if (lootTable.LeveledLoot.Any(x => x.Level == level))
                {
                    EpicLoot.LogWarning($"Duplicated leveled loot for ({lootTable.Object} lvl {level}), using 'Loot'");
                }
                return lootTable.Loot.ToArray();
            }

            for (var lvl = level; lvl >= 1; --lvl)
            {
                var found = lootTable.LeveledLoot.Find(x => x.Level == lvl);
                if (found != null && !ArrayUtils.IsNullOrEmpty(found.Loot))
                {
                    return found.Loot.ToArray();
                }

                if (!useNextHighestIfNotPresent)
                {
                    return null;
                }
            }

            EpicLoot.LogError($"Could not find any leveled loot for ({lootTable.Object} lvl {level}), but a loot table exists for this object!");
            return null;
        }

        public static List<MagicItemEffect> RollAugmentEffects(ExtendedItemData item, MagicItem magicItem, int effectIndex)
        {
            var results = new List<MagicItemEffect>();

            var rarity = magicItem.Rarity;
            var currentEffect = magicItem.Effects[effectIndex];
            results.Add(currentEffect);

            var valuelessEffect = MagicItemEffectDefinitions.IsValuelessEffect(currentEffect.EffectType, rarity);
            var availableEffects = MagicItemEffectDefinitions.GetAvailableEffects(item, magicItem, valuelessEffect ? -1 : effectIndex);

            for (var i = 0; i < 2 && i < availableEffects.Count; i++)
            {
                var newEffect = RollEffects(availableEffects, rarity, 1, false).FirstOrDefault();
                if (newEffect == null)
                {
                    EpicLoot.LogError($"Rolled a null effect: item:{item.m_shared.m_name}, index:{effectIndex}");
                    continue;
                }

                results.Add(newEffect);

                var newEffectIsValueless = MagicItemEffectDefinitions.IsValuelessEffect(newEffect.EffectType, rarity);
                if (newEffectIsValueless)
                {
                    availableEffects.RemoveAll(x => x.Type == newEffect.EffectType);
                }
            }

            return results;
        }

        public static void AddDebugMagicEffects(MagicItem item)
        {
            if (!string.IsNullOrEmpty(ForcedMagicEffect) && !item.HasEffect(ForcedMagicEffect))
            {
                EpicLoot.Log($"AddDebugMagicEffect {ForcedMagicEffect}");
                item.Effects.Add(RollEffect(MagicItemEffectDefinitions.Get(ForcedMagicEffect), item.Rarity));
            }
        }
    }
}
