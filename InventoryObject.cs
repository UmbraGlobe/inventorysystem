using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/New Inventory")]
public class InventoryObject : SerializableSO
{
    [SerializeField] private string savePath;
    [SerializeField] private int inventorySize;
    [SerializeField] private EventVoid OnItemsUpdated;
    [SerializeField] private EventVoid OnPlayerStatsUpdated;
    [SerializeField] private ItemStack[] Inventory = new ItemStack[0];

    public int InventorySize => inventorySize;

    private void InitInventory()
    {
        Inventory = new ItemStack[inventorySize];
        for (int i = 0; i < Inventory.Length; i++)
        {
            Inventory[i] = new ItemStack();
        }
        OnItemsUpdated.Raise(null);
    }
    #region Json
    public override void JsonLoad()
    {
        //pair of item instance and index
        List<KeyValuePair<ItemSaveData, int>> data = SaveSystem.Instance.LoadData<List<KeyValuePair<ItemSaveData, int>>>(savePath);
        if (data == null) { Debug.LogError("Failed to load inventory"); return; }

        for (int i = 0; i < data.Count; i++)
        {
            ItemInstance item = ItemDatabase.RebuildItem(data[i].Key);
            AddAtIndex(new ItemStack(item, data[i].Key.quantity), data[i].Value);
        }
        OnItemsUpdated.Raise(null);
    }
    public override void JsonSave()
    {
        List<KeyValuePair<ItemSaveData, int>> data = new List<KeyValuePair<ItemSaveData, int>>();

        for (int i = 0; i < Inventory.Length; i++)
        {
            if (Inventory[i] == null) { Debug.Log("tried to save null item, there is an issue"); continue; }
            if (Inventory[i].Item == null) continue;

            if (Inventory[i].Item is EquipmentInstance)
            {
                GenericDictionary<ItemStat, float> stats = new GenericDictionary<ItemStat, float>();
                foreach (var stat in (Inventory[i].Item as EquipmentInstance).Affixes)
                {
                    stats.Add(stat.Key.stat, stat.Value);
                } 

                ItemSaveData saveData = new ItemSaveData(
                    Inventory[i].Item.BaseItem.GUID,
                    Inventory[i].Quantity,
                    Inventory[i].Item.Rarity.RarityType,
                    stats,
                    Inventory[i].Item.SellPrice,
                    (Inventory[i].Item as EquipmentInstance).ItemLevel
                    );

                data.Add(new KeyValuePair<ItemSaveData, int>(saveData, i));
            }
            else
            {
                ItemSaveData saveData = new ItemSaveData(
                    Inventory[i].Item.BaseItem.GUID, 
                    Inventory[i].Quantity, 
                    Inventory[i].Item.Rarity.RarityType);
                data.Add(new KeyValuePair<ItemSaveData, int>(saveData, i));
            }
        }
        if (!SaveSystem.Instance.SaveData(savePath, data)) { Debug.LogError("Failed to save inventory"); }
    }

    public override void JsonReset()
    {
        InitInventory();
    }

    #endregion
    #region Helper

    public ItemStack[] GetItemList()
    {
        return Inventory;
    }

    public ItemStack GetItemAtIndex(int index)
    {
        return Inventory[index];
    }

    private bool IsThereSpaceForItem(ItemInstance _item, int _amount)
    {
        if (FirstOccuranceOfItemFree(_item) != -1) return true;
        if (FirstEmptySlot() != -1) return true;
        return false;
    }

    private int LastOccuranceOfItem(ItemInstance _item)
    {
        for (int i = Inventory.Length - 1; i > 0; i--)
        {
            if (Inventory[i].Item == _item)
            {
                return i;
            }
        }
        return -1;
    }

    public int FirstEmptySlot()
    {
        for (int i = 0; i < Inventory.Length; i++)
        {
            if (Inventory[i].Item == null)
            {
                return i;
            }
        }
        return -1;
    }

    public int RemainingSpaceInSlot(int index)
    {
        return Inventory[index].Item.BaseItem.MaxStack - Inventory[index].Quantity;
    }

    public int ItemCount()
    {   
        int count = 0;
        for (int i = 0; i < Inventory.Length; i++)
        {
            if (Inventory[i].Item != null)
            {
                count++;
            }
        }
        return count;
    }

    private int FirstOccuranceOfItemFree(ItemInstance _item)
    {

        for (int i = 0; i < Inventory.Length; i++)
        {
            if (Inventory[i].Item == null) continue;
            if (Inventory[i].Item.BaseItem == _item.BaseItem && Inventory[i].Quantity != Inventory[i].Item.BaseItem.MaxStack)
            {
                return i;
            }
        }
        return -1;
    }
    #endregion
    #region Functional
    public bool AddItem(ItemInstance _item, int _amount)
    {
        if (_item == null)
        {
            Debug.Log("Trying to add null item");
            return false;
        }
        if (!IsThereSpaceForItem(_item, _amount))
        {
            //Debug.Log("No Space");
            return false;
        }

        if (_amount > _item.BaseItem.MaxStack)
        {
            for(int i = 0; i < _amount; i++)
            {
                AddItem(_item, 1);
            }
            return true;
        }

        int itemIndex = FirstOccuranceOfItemFree(_item);
        if (itemIndex != -1) //exists
        {   
            if (RemainingSpaceInSlot(itemIndex) >= _amount)
            {
                Inventory[itemIndex].AddAmount(_amount);
                OnItemsUpdated.Raise(null);
                return true;
            }
            else if (FirstEmptySlot() != -1)
            {
                Inventory[FirstEmptySlot()] = new ItemStack(_item, _amount);
                OnItemsUpdated.Raise(null);
                return true;
            }
        }

        if (FirstEmptySlot() == -1) return false;
        Inventory[FirstEmptySlot()] = new ItemStack(_item, _amount);
        OnItemsUpdated.Raise(null);
        return true;
    }

    public bool RemoveItem(ItemInstance _item, int _amount)
    {
        if (_amount > _item.BaseItem.MaxStack)
        {
            for(int i = 0; i < _amount; i++)
            {
                RemoveItem(_item, 1);
            }
            return true;
        }

        int itemIndex = LastOccuranceOfItem(_item);
        if (itemIndex != -1)
        {   
            if (Inventory[itemIndex].Quantity > _amount)
            {
                Inventory[itemIndex].AddAmount(-_amount);
                OnItemsUpdated.Raise(null);
                return true;
            }
            else
            {
                Inventory[itemIndex] = new ItemStack();
                OnItemsUpdated.Raise(null);
                return true;
            }
        }
        return false;
    }

    public void Swap(int indexOne, int indexTwo)
    {
        if (indexOne == indexTwo) {OnItemsUpdated.Raise(null); return;}

        ItemStack itemOne = Inventory[indexOne];
        ItemStack itemTwo = Inventory[indexTwo];

        if (itemTwo.Item == null)
        {
            Inventory[indexTwo] = Inventory[indexOne];
            Inventory[indexOne] = new ItemStack();
        }
        else if (itemOne.Item.BaseItem == itemTwo.Item.BaseItem)
        {
            if (itemOne.Quantity + itemTwo.Quantity <= itemOne.Item.BaseItem.MaxStack)
            {
                Inventory[indexTwo].AddAmount(itemOne.Quantity);
                Inventory[indexOne] = new ItemStack();
            }
            else
            {
                int left = RemainingSpaceInSlot(indexTwo);

                Inventory[indexTwo].AddAmount(left);
                Inventory[indexOne].AddAmount(-left);
            }
        }
        else if (itemOne.Item != itemTwo.Item)
        {
            Inventory[indexTwo] = itemOne;
            Inventory[indexOne] = itemTwo;
        }
        OnItemsUpdated.Raise(null);
    }
    
    public void Swap(int indexOne, int indexTwo, ref InventoryObject otherInventory)
    {
        ItemStack itemOne = Inventory[indexOne];
        ItemStack itemTwo = otherInventory.Inventory[indexTwo];

        if (itemTwo.Item == null)
        {
            otherInventory.Inventory[indexTwo] = Inventory[indexOne];
            Inventory[indexOne] = new ItemStack();
        }
        else if (itemOne.Item == itemTwo.Item)
        {
            if (itemOne.Quantity + itemTwo.Quantity <= itemOne.Item.BaseItem.MaxStack)
            {
                otherInventory.Inventory[indexTwo].AddAmount(itemOne.Quantity);
                Inventory[indexOne] = new ItemStack();
            }
            else
            {
                int left = otherInventory.RemainingSpaceInSlot(indexTwo);

                otherInventory.Inventory[indexTwo].AddAmount(left);
                Inventory[indexOne].AddAmount(-left);
            }
        }
        else if (itemOne.Item != itemTwo.Item)
        {
            otherInventory.Inventory[indexTwo] = itemOne;
            Inventory[indexOne] = itemTwo;
        }
        OnItemsUpdated.Raise(null);
    }

    public void SwapToGear(int indexOne, int indexTwo, ref InventoryObject otherInventory)
    {
        Swap(indexOne, indexTwo, ref otherInventory);
        Debug.Log("Equipped");
        OnPlayerStatsUpdated?.Raise(null);
    }
    public void RemoveAtIndex(int index)
    {
        Inventory[index] = new ItemStack();
        OnItemsUpdated.Raise(null);
    }
    public void AddAtIndex(ItemStack item, int index)
    {
        if (item.Item == null)
        {
            Debug.Log("Trying to add null item at index");
            return;
        }
        Inventory[index] = item;
        OnItemsUpdated.Raise(null);
    }
    #endregion
    #region Test
    [ContextMenu("Print inventory")]
    public void PrintInventory()
    {
        for (int i = 0; i < Inventory.Length; i++)
        {
            Debug.Log($"{Inventory[i].Quantity} {Inventory[i].Item} at index {i}");
        }
    }


    [ContextMenu("Clear Inventory")]
    public void ClearInventory()
    {
        InitInventory();
    }

    #endregion
    
}
