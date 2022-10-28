using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using IslandSanctuaryEarningsReport.Attributes;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentList;
using static Lumina.Data.Parsing.Uld.NodeData;

namespace IslandSanctuaryEarningsReport
{
    public class Plugin : IDalamudPlugin
    {
        private readonly DalamudPluginInterface pluginInterface;
        private readonly ChatGui chat;
        private readonly ClientState clientState;
        private readonly GameGui gameGui;

        private readonly PluginCommandManager<Plugin> commandManager;

        public string Name => "Your Plugin's Display Name";

        public Plugin(
            DalamudPluginInterface pi,
            CommandManager commands,
            ChatGui chat,
            ClientState clientState,
            GameGui gameGui)
        {
            this.pluginInterface = pi;
            this.chat = chat;
            this.clientState = clientState;
            this.gameGui = gameGui;

            // Load all of our commands
            this.commandManager = new PluginCommandManager<Plugin>(this, commands);
        }

        [Command("/earnings")]
        public void ExampleCommand1(string command, string args)
        {
            var (day, crafts) = FetchCrafts();
            if (day > 0)
            {
                chat.Print($"Crafts for Day {day}:");
                if (crafts.Count > 0)
                {
                    foreach (var craft in crafts)
                        chat.Print($"  {craft.Quantity}x {craft.Product}");
                }
                else
                    chat.Print("  No crafts.");
            }
        }

        #region Read Earnings Screen

        internal unsafe (uint Day, List<Craft> Crafts) FetchCrafts()
        {
            try
            {
                IntPtr addonPtr = gameGui.GetAddonByName("MJICraftSales", 1);
                if (addonPtr == IntPtr.Zero)
                {
                    chat.PrintError("Earnings Report is not open.");
                    return (0, new());
                }

                AddonMJICraftSales* addon = (AddonMJICraftSales*)addonPtr;
                AtkResNode* rootNode = addon->AtkUnitBase.RootNode;
                if (rootNode == null)
                {
                    chat.PrintError("Root node not found.");
                    return (0, new());
                }

                uint day = 0;
                for (uint i = 1; i <= 7; ++i)
                {
                    var radioNode = (AtkComponentNode*)addon->AtkUnitBase.GetNodeById(17 + i);
                    if (radioNode == null)
                        continue;

                    var radio = (AtkComponentButton*)radioNode->Component;
                    if (radio == null || (radio->Flags & 0x40000) == 0)
                        continue;

                    day = i;
                    break;
                }

                var listNode = (AtkComponentNode*)addon->AtkUnitBase.GetNodeById(32);
                if (listNode == null || !listNode->AtkResNode.IsVisible)
                    return (day, new());

                List<Craft> crafts = new();
                var list = (AtkComponentList*)listNode->Component;
                for (int i = 0; i < list->ListLength; ++i)
                {
                    var itemNode = list->ItemRendererList[i].AtkComponentListItemRenderer;
                    var uldManager = itemNode->AtkComponentButton.AtkComponentBase.UldManager;
                    var productNode = GetNodeByID<AtkTextNode>(uldManager, 3, NodeType.Text);
                    if (productNode == null)
                        continue;

                    var quantityNode = GetNodeByID<AtkTextNode>(uldManager, 4, NodeType.Text);
                    if (quantityNode == null)
                        continue;

                    crafts.Add(new Craft
                    {
                        Product = productNode->NodeText.ToString(),
                        Quantity = int.Parse(quantityNode->NodeText.ToString()),
                    });
                }
                return (day, crafts);
            }
            catch (Exception e)
            {
                chat.PrintError(e.ToString());
                return (0, new());
            }
        }

        public unsafe T* GetNodeByID<T>(AtkUldManager uldManager, uint nodeId, NodeType? type = null) where T : unmanaged
        {
            for (var i = 0; i < uldManager.NodeListCount; i++)
            {
                var n = uldManager.NodeList[i];
                if (n->NodeID != nodeId || type != null && n->Type != type.Value) continue;
                if (!n->IsVisible) continue;
                return (T*)n;
            }
            return null;
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x4)]
        internal unsafe struct AddonMJICraftSales
        {
            [FieldOffset(0x0)] public AtkUnitBase AtkUnitBase;
        }

        internal struct Craft
        {
            public string Product { get; set; }
            public int Quantity { get; set; }
        }
        #endregion

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
