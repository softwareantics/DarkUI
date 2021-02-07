﻿using DarkUI.Controls;
using DarkUI.Forms;

namespace Example
{
    public partial class DialogControls : DarkDialog
    {
        public DialogControls()
        {
            this.InitializeComponent();

            // Build dummy list data
            for (var i = 0; i < 100; i++)
            {
                var item = new DarkListItem($"List item #{i}");
                this.lstTest.Items.Add(item);
            }

            // Build dummy nodes
            var childCount = 0;
            for (var i = 0; i < 20; i++)
            {
                var node = new DarkTreeNode($"Root node #{i}");

                for (var x = 0; x < 10; x++)
                {
                    var childNode = new DarkTreeNode($"Child node #{childCount}");
                    childCount++;
                    node.Nodes.Add(childNode);
                }

                this.treeTest.Nodes.Add(node);
            }

            // Hook dialog button events
            this.btnDialog.Click += delegate
            {
                DarkMessageBox.ShowError("This is an error", "Dark UI - Example");
            };

            this.btnMessageBox.Click += delegate
            {
                DarkMessageBox.ShowInformation("This is some information, except it is much bigger, so there we go. I wonder how this is going to go. I hope it resizes properly. It probably will.", "Dark UI - Example");
            };
        }
    }
}