﻿using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using EpicLoot.Crafting;
using EpicLoot.LegendarySystem;
using ExtendedItemDataFramework;
using fastJSON;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EpicLoot
{
    public class MagicItemComponent : BaseExtendedItemComponent
    {
        public MagicItem MagicItem;

        private static readonly JSONParameters _saveParams = new JSONParameters { UseExtensions = false };

        public MagicItemComponent(ExtendedItemData parent) 
            : base(typeof(MagicItemComponent).AssemblyQualifiedName, parent)
        {
        }

        public void SetMagicItem(MagicItem magicItem)
        {
            MagicItem = magicItem;
            Save();
        }

        public override string Serialize()
        {
            return JSON.ToJSON(MagicItem, _saveParams);
        }

        public override void Deserialize(string data)
        {
            try
            {
                MagicItem = JSON.ToObject<MagicItem>(data, _saveParams);
            }
            catch (Exception)
            {
                EpicLoot.LogError($"[{nameof(MagicItemComponent)}] Could not deserialize MagicItem json data! ({ItemData?.m_shared?.m_name})");
                throw;
            }
        }

        public override BaseExtendedItemComponent Clone()
        {
            return MemberwiseClone() as BaseExtendedItemComponent;
        }

        public static void OnNewExtendedItemData(ExtendedItemData itemdata)
        {
            if (itemdata.m_shared.m_name == "$item_helmet_dverger")
            {
                var magicItem = new MagicItem();
                magicItem.Rarity = ItemRarity.Rare;
                magicItem.Effects.Add(new MagicItemEffect(MagicEffectType.DvergerCirclet));
                magicItem.TypeNameOverride = "circlet";

                itemdata.ReplaceComponent<MagicItemComponent>().MagicItem = magicItem;
            }
            else if (itemdata.m_shared.m_name == "$item_beltstrength")
            {
                var magicItem = new MagicItem();
                magicItem.Rarity = ItemRarity.Rare;
                magicItem.Effects.Add(new MagicItemEffect(MagicEffectType.Megingjord));
                magicItem.TypeNameOverride = "belt";

                itemdata.ReplaceComponent<MagicItemComponent>().MagicItem = magicItem;
            }
            else if (itemdata.m_shared.m_name == "$item_wishbone")
            {
                var magicItem = new MagicItem();
                magicItem.Rarity = ItemRarity.Epic;
                magicItem.Effects.Add(new MagicItemEffect(MagicEffectType.Wishbone));
                magicItem.TypeNameOverride = "remains";

                itemdata.ReplaceComponent<MagicItemComponent>().MagicItem = magicItem;
            }
        }
    }

    public static class ItemDataExtensions
    {
        public static bool IsMagic(this ItemDrop.ItemData itemData)
        {
            return itemData.Extended()?.GetComponent<MagicItemComponent>() != null;
        }

        public static bool IsMagic(this ItemDrop.ItemData itemData, out MagicItem magicItem)
        {
            magicItem = itemData.GetMagicItem();
            return magicItem != null;
        }

        public static bool UseMagicBackground(this ItemDrop.ItemData itemData)
        {
            return itemData.IsMagic() || itemData.IsRunestone();
        }

        public static ItemRarity GetRarity(this ItemDrop.ItemData itemData)
        {
            if (itemData.IsMagic())
            {
                return itemData.GetMagicItem().Rarity;
            }
            else if (itemData.IsMagicCraftingMaterial())
            {
                return itemData.GetCraftingMaterialRarity();
            }
            else if (itemData.IsRunestone())
            {
                return itemData.GetRunestoneRarity();
            }

            throw new ArgumentException("itemData is not magic item, magic crafting material, or runestone");
        }

        public static Color GetRarityColor(this ItemDrop.ItemData itemData)
        {
            var colorString = "white";
            if (itemData.IsMagic())
            {
                colorString = itemData.GetMagicItem().GetColorString();
            }
            else if (itemData.IsMagicCraftingMaterial())
            {
                colorString = itemData.GetCraftingMaterialRarityColor();
            }
            else if (itemData.IsRunestone())
            {
                colorString = itemData.GetRunestoneRarityColor();
            }

            return ColorUtility.TryParseHtmlString(colorString, out var color) ? color : Color.white;
        }

        public static bool HasMagicEffect(this ItemDrop.ItemData itemData, string effectType)
        {
            return itemData.GetMagicItem()?.HasEffect(effectType) ?? false;
        }

        public static MagicItem GetMagicItem(this ItemDrop.ItemData itemData)
        {
            return itemData.Extended()?.GetComponent<MagicItemComponent>()?.MagicItem;
        }

        public static string GetDecoratedName(this ItemDrop.ItemData itemData, string colorOverride = null)
        {
            var color = "white";
            var name = itemData.m_shared.m_name;

            if (itemData.IsMagic())
            {
                var magicItem = itemData.GetMagicItem();
                color = magicItem.GetColorString();
                if (!string.IsNullOrEmpty(magicItem.DisplayName))
                {
                    name = magicItem.DisplayName;
                }
            }
            else if (itemData.IsMagicCraftingMaterial() || itemData.IsRunestone())
            {
                color = itemData.GetCraftingMaterialRarityColor();
            }

            if (!string.IsNullOrEmpty(colorOverride))
            {
                color = colorOverride;
            }

            return $"<color={color}>{name}</color>";
        }

        public static string GetDescription(this ItemDrop.ItemData itemData)
        {
            if (itemData.IsMagic())
            {
                var magicItem = itemData.GetMagicItem();
                if (magicItem.IsUniqueLegendary() && UniqueLegendaryHelper.TryGetLegendaryInfo(magicItem.LegendaryID, out var legendaryInfo))
                {
                    return legendaryInfo.Description;
                }
            }
            return itemData.m_shared.m_description;
        }

        public static bool IsPartOfSet(this ItemDrop.ItemData itemData, string setName)
        {
            return itemData.GetSetID() == setName;
        }

        public static bool CanBeAugmented(this ItemDrop.ItemData itemData)
        {
            if (!itemData.IsMagic())
            {
                return false;
            }

            return itemData.GetMagicItem().Effects.Select(effect => MagicItemEffectDefinitions.Get(effect.EffectType)).Any(effectDef => effectDef.CanBeAugmented);
        }

        public static string GetSetID(this ItemDrop.ItemData itemData, out bool isMundane)
        {
            isMundane = true;
            if (itemData.IsMagic(out var magicItem) && !string.IsNullOrEmpty(magicItem.SetID))
            {
                isMundane = false;
                return magicItem.SetID;
            }

            if (!string.IsNullOrEmpty(itemData.m_shared.m_setName))
            {
                return itemData.m_shared.m_setName;
            }

            return null;
        }

        public static string GetSetID(this ItemDrop.ItemData itemData)
        {
            return GetSetID(itemData, out _);
        }

        public static LegendarySetInfo GetLegendarySetInfo(this ItemDrop.ItemData itemData)
        {
            UniqueLegendaryHelper.TryGetLegendarySetInfo(itemData.GetSetID(), out var setInfo);
            return setInfo;
        }

        public static bool IsSetItem(this ItemDrop.ItemData itemData)
        {
            return !string.IsNullOrEmpty(itemData.GetSetID());
        }

        public static bool IsLegendarySetItem(this ItemDrop.ItemData itemData)
        {
            return itemData.IsMagic(out var magicItem) && !string.IsNullOrEmpty(magicItem.SetID);
        }

        public static bool IsMundaneSetItem(this ItemDrop.ItemData itemData)
        {
            return !string.IsNullOrEmpty(itemData.m_shared.m_setName);
        }

        public static int GetSetSize(this ItemDrop.ItemData itemData)
        {
            var setID = itemData.GetSetID(out var isMundane);
            if (!string.IsNullOrEmpty(setID))
            {
                if (isMundane)
                {
                    return itemData.m_shared.m_setSize;
                }
                else if (UniqueLegendaryHelper.TryGetLegendarySetInfo(setID, out var setInfo))
                {
                    return setInfo.LegendaryIDs.Count;
                }
            }

            return 0;
        }

        public static List<string> GetSetPieces(string setName)
        {
            if (UniqueLegendaryHelper.TryGetLegendarySetInfo(setName, out var setInfo))
            {
                return setInfo.LegendaryIDs;
            }

            return GetMundaneSetPieces(ObjectDB.instance, setName);
        }

        public static List<string> GetMundaneSetPieces(ObjectDB objectDB, string setName)
        {
            var results = new List<string>();
            foreach (var itemPrefab in objectDB.m_items)
            {
                if (itemPrefab == null)
                {
                    EpicLoot.LogError("Null Item left in ObjectDB! (This means that a prefab was deleted and not an instance)");
                    continue;
                }

                var itemDrop = itemPrefab.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    EpicLoot.LogError($"Item in ObjectDB missing ItemDrop: ({itemPrefab.name})");
                    continue;
                }

                if (itemDrop.m_itemData.m_shared.m_setName == setName)
                {
                    results.Add(itemPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name);
                }
            }

            return results;
        }
    }

    public static class PlayerExtensions
    {
        public static List<ItemDrop.ItemData> GetEquipment(this Player player)
        {
            var results = new List<ItemDrop.ItemData>();
            if (player.m_rightItem != null)
                results.Add(player.m_rightItem);
            if (player.m_leftItem != null)
                results.Add(player.m_leftItem);
            if (player.m_chestItem != null)
                results.Add(player.m_chestItem);
            if (player.m_legItem != null)
                results.Add(player.m_legItem);
            if (player.m_helmetItem != null)
                results.Add(player.m_helmetItem);
            if (player.m_shoulderItem != null)
                results.Add(player.m_shoulderItem);
            if (player.m_utilityItem != null)
                results.Add(player.m_utilityItem);
            return results;
        }

        private static List<ItemDrop.ItemData> GetMagicEquipmentWithEffect(this Player player, string effectType)
        {
            return player.GetEquipment().Where(x => x.HasMagicEffect(effectType)).ToList();
        }

        public static List<MagicItemEffect> GetAllActiveMagicEffects(this Player player, string effectType = null)
        {
            var equipEffects = player.GetEquipment().Where(x => x.IsMagic())
                .SelectMany(x => x.GetMagicItem().GetEffects(effectType));
            var setEffects = player.GetAllActiveSetMagicEffects(effectType);
            return equipEffects.Concat(setEffects).ToList();
        }

        public static List<MagicItemEffect> GetAllActiveSetMagicEffects(this Player player, string effectType = null)
        {
            var activeSetEffects = new List<MagicItemEffect>();
            var equippedSets = player.GetEquippedSets();
            foreach (var setInfo in equippedSets)
            {
                var count = player.GetEquippedSetPieces(setInfo.ID).Count;
                foreach (var setBonusInfo in setInfo.SetBonuses)
                {
                    if (count >= setBonusInfo.Count && (effectType == null || setBonusInfo.Effect.Type == effectType))
                    {
                        var effect = new MagicItemEffect(setBonusInfo.Effect.Type, setBonusInfo.Effect.Values?.MinValue ?? 0);
                        activeSetEffects.Add(effect);
                    }
                }
            }

            return activeSetEffects;
        }

        public static HashSet<LegendarySetInfo> GetEquippedSets(this Player player)
        {
            var sets = new HashSet<LegendarySetInfo>();
            foreach (var itemData in player.GetEquipment())
            {
                if (itemData.IsMagic(out var magicItem) && magicItem.IsLegendarySetItem())
                {
                    if (UniqueLegendaryHelper.TryGetLegendarySetInfo(magicItem.SetID, out var setInfo))
                    {
                        sets.Add(setInfo);
                    }
                }
            }

            return sets;
        }

        public static float GetTotalActiveMagicEffectValue(this Player player, string effectType, float scale = 1.0f)
        {
            return player.GetAllActiveMagicEffects(effectType)
                .Select(x => x.EffectValue)
                .DefaultIfEmpty(0)
                .Sum() * scale;
        }

        public static bool HasActiveMagicEffect(this Player player, string effectType)
        {
            return GetMagicEquipmentWithEffect(player, effectType).Count > 0 || player.GetAllActiveSetMagicEffects(effectType).Count > 0;
        }

        private static bool HasMagicEquipmentWithEffect(this Player player, string effectType, out List<ItemDrop.ItemData> equipment)
        {
            equipment = GetMagicEquipmentWithEffect(player, effectType);
            return equipment.Count > 0;
        }

        public static List<ItemDrop.ItemData> GetEquippedSetPieces(this Player player, string setName)
        {
            return player.GetEquipment().Where(x => x.IsPartOfSet(setName)).ToList();
        }

        public static bool HasEquipmentOfType(this Player player, ItemDrop.ItemData.ItemType type)
        {
            return player.GetEquipment().Exists(x => x != null && x.m_shared.m_itemType == type);
        }

        public static ItemDrop.ItemData GetEquipmentOfType(this Player player, ItemDrop.ItemData.ItemType type)
        {
            return player.GetEquipment().FirstOrDefault(x => x != null && x.m_shared.m_itemType == type);
        }

        public static Player GetPlayerWithEquippedItem(ItemDrop.ItemData itemData)
        {
            return Player.m_players.FirstOrDefault(player => player.IsItemEquiped(itemData));
        }
    }

    public static class ItemBackgroundHelper
    {
        public static Image CreateAndGetMagicItemBackgroundImage(GameObject elementGo, GameObject equipped, bool addSetItem)
        {
            var magicItemTransform = elementGo.transform.Find("magicItem");
            if (magicItemTransform == null)
            {
                var magicItemObject = Object.Instantiate(equipped, equipped.transform.parent);
                magicItemObject.transform.SetSiblingIndex(equipped.transform.GetSiblingIndex() + 1);
                magicItemObject.name = "magicItem";
                magicItemObject.SetActive(true);
                magicItemTransform = magicItemObject.transform;
                var magicItemInit = magicItemTransform.GetComponent<Image>();
                magicItemInit.color = Color.white;
                magicItemInit.raycastTarget = false;
            }

            // Also add set item marker
            if (addSetItem)
            {
                var setItemTransform = elementGo.transform.Find("setItem");
                if (setItemTransform == null)
                {
                    var setItemObject = Object.Instantiate(equipped, equipped.transform.parent);
                    setItemObject.transform.SetAsLastSibling();
                    setItemObject.name = "setItem";
                    setItemObject.SetActive(true);
                    setItemTransform = setItemObject.transform;
                    var setItemInit = setItemTransform.GetComponent<Image>();
                    setItemInit.raycastTarget = false;
                    setItemInit.sprite = EpicLoot.Assets.GenericSetItemSprite;
                    setItemInit.color = ColorUtility.TryParseHtmlString(EpicLoot.GetSetItemColor(), out var color) ? color : Color.white;
                }
            }

            // Also change equipped image
            var equippedImage = equipped.GetComponent<Image>();
            if (equippedImage != null)
            {
                equippedImage.sprite = EpicLoot.Assets.EquippedSprite;
                equippedImage.color = Color.white;
                equippedImage.raycastTarget = false;
                var rectTransform = equipped.RectTransform();
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, equippedImage.sprite.texture.width);
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, equippedImage.sprite.texture.height);
            }

            return magicItemTransform.GetComponent<Image>();
        }
    }

    //public void UpdateGui(Player player, ItemDrop.ItemData dragItem)
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    public static class InventoryGrid_UpdateGui_MagicItemComponent_Patch
    {
        public static void Postfix(InventoryGrid __instance, Player player, ItemDrop.ItemData dragItem)
        {
            foreach (var element in __instance.m_elements)
            {
                var magicItemTransform = element.m_go.transform.Find("magicItem");
                if (magicItemTransform != null)
                {
                    var magicItem = magicItemTransform.GetComponent<Image>();
                    if (magicItem != null)
                    {
                        magicItem.enabled = false;
                    }
                }

                var setItemTransform = element.m_go.transform.Find("setItem");
                if (setItemTransform != null)
                {
                    var setItem = setItemTransform.GetComponent<Image>();
                    if (setItem != null)
                    {
                        setItem.enabled = false;
                    }
                }
            }

            foreach (var item in __instance.m_inventory.m_inventory)
            {
                var element = __instance.GetElement(item.m_gridPos.x, item.m_gridPos.y, __instance.m_inventory.GetWidth());
                if (element == null)
                {
                    EpicLoot.LogError($"Could not find element for item ({item.m_shared.m_name}: {item.m_gridPos}) in inventory: {__instance.m_inventory.m_name}");
                    continue;
                }

                var magicItem = ItemBackgroundHelper.CreateAndGetMagicItemBackgroundImage(element.m_go, element.m_equiped.gameObject, true);
                if (item.UseMagicBackground())
                {
                    magicItem.enabled = true;
                    magicItem.sprite = EpicLoot.GetMagicItemBgSprite();
                    magicItem.color = item.GetRarityColor();
                }

                var setItemTransform = element.m_go.transform.Find("setItem");
                if (setItemTransform != null)
                {
                    var setItem = setItemTransform.GetComponent<Image>();
                    if (setItem != null)
                    {
                        setItem.enabled = item.IsSetItem();
                    }
                }
            }
        }
    }

    //void UpdateIcons(Player player)
    [HarmonyPatch(typeof(HotkeyBar), nameof(HotkeyBar.UpdateIcons), typeof(Player))]
    public static class HotkeyBar_UpdateIcons_Patch
    {
        public static void Postfix(HotkeyBar __instance, List<HotkeyBar.ElementData> ___m_elements, List<ItemDrop.ItemData> ___m_items, Player player)
        {
            if (player == null || player.IsDead())
            {
                return;
            }

            for (var index = 0; index < ___m_elements.Count; index++)
            {
                var element = ___m_elements[index];
                var magicItem = ItemBackgroundHelper.CreateAndGetMagicItemBackgroundImage(element.m_go, element.m_equiped, false);
                magicItem.enabled = false;
            }

            for (var index = 0; index < ___m_items.Count; ++index)
            {
                var itemData = ___m_items[index];
                var element = GetElementForItem(___m_elements, itemData);
                if (element == null)
                {
                    EpicLoot.LogWarning($"Tried to get element for {itemData.m_shared.m_name} at {itemData.m_gridPos}, but element was null (total elements = {___m_elements.Count})");
                    continue;
                }

                var magicItem = ItemBackgroundHelper.CreateAndGetMagicItemBackgroundImage(element.m_go, element.m_equiped, false);
                if (itemData.UseMagicBackground())
                {
                    magicItem.enabled = true;
                    magicItem.sprite = EpicLoot.GetMagicItemBgSprite();
                    magicItem.color = itemData.GetRarityColor();
                }
            }
        }

        private static HotkeyBar.ElementData GetElementForItem(List<HotkeyBar.ElementData> elements, ItemDrop.ItemData item)
        {
            var index = item.m_gridPos.y == 0 
                ? item.m_gridPos.x 
                : Player.m_localPlayer.GetInventory().m_width + item.m_gridPos.x - 5;

            return index >= 0 && index < elements.Count ? elements[index] : null;
        }
    }
}
