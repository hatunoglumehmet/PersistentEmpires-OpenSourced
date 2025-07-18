using PersistentEmpiresLib.NetworkMessages.Client;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace PersistentEmpiresLib.NetworkMessages.Client
{
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestItemBrowser : GameNetworkMessage
    {
        public string Category { get; set; }
        public string SearchTerm { get; set; }
        public int Page { get; set; }

        public RequestItemBrowser() { }
        public RequestItemBrowser(string category, string searchTerm, int page)
        {
            Category = category ?? "";
            SearchTerm = searchTerm ?? "";
            Page = page;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Administration;
        }

        protected override string OnGetLogFormat()
        {
            return "Received RequestItemBrowser";
        }

        protected override bool OnRead()
        {
            bool result = true;
            Category = GameNetworkMessage.ReadStringFromPacket(ref result);
            SearchTerm = GameNetworkMessage.ReadStringFromPacket(ref result);
            Page = GameNetworkMessage.ReadIntFromPacket(CompressionMission.AutomatedBattleIndexCompressionInfo, ref result);
            return result;
        }

        protected override void OnWrite()
        {
            GameNetworkMessage.WriteStringToPacket(Category);
            GameNetworkMessage.WriteStringToPacket(SearchTerm);
            GameNetworkMessage.WriteIntToPacket(Page, CompressionMission.AutomatedBattleIndexCompressionInfo);
        }
    }

    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestMassItemSpawn : GameNetworkMessage
    {
        public string ItemId { get; set; }
        public int Count { get; set; }
        public bool SpawnAtFeet { get; set; }

        public RequestMassItemSpawn() { }
        public RequestMassItemSpawn(string itemId, int count, bool spawnAtFeet)
        {
            ItemId = itemId;
            Count = count;
            SpawnAtFeet = spawnAtFeet;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Administration;
        }

        protected override string OnGetLogFormat()
        {
            return "Received RequestMassItemSpawn";
        }

        protected override bool OnRead()
        {
            bool result = true;
            ItemId = GameNetworkMessage.ReadStringFromPacket(ref result);
            Count = GameNetworkMessage.ReadIntFromPacket(CompressionMission.ItemDataCompressionInfo, ref result);
            SpawnAtFeet = GameNetworkMessage.ReadBoolFromPacket(ref result);
            return result;
        }

        protected override void OnWrite()
        {
            GameNetworkMessage.WriteStringToPacket(ItemId);
            GameNetworkMessage.WriteIntToPacket(Count, CompressionMission.ItemDataCompressionInfo);
            GameNetworkMessage.WriteBoolToPacket(SpawnAtFeet);
        }
    }

    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class RequestItemInfo : GameNetworkMessage
    {
        public string ItemId { get; set; }

        public RequestItemInfo() { }
        public RequestItemInfo(string itemId)
        {
            ItemId = itemId;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.Administration;
        }

        protected override string OnGetLogFormat()
        {
            return "Received RequestItemInfo";
        }

        protected override bool OnRead()
        {
            bool result = true;
            ItemId = GameNetworkMessage.ReadStringFromPacket(ref result);
            return result;
        }

        protected override void OnWrite()
        {
            GameNetworkMessage.WriteStringToPacket(ItemId);
        }
    }
}