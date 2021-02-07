using DarkUI.Controls;
using DarkUI.Docking;

namespace Example
{
    public partial class DockLayers : DarkToolWindow
    {
        public DockLayers()
        {
            this.InitializeComponent();

            // Build dummy list data
            for (var i = 0; i < 100; i++)
            {
                var item = new DarkListItem($"List item #{i}");
                this.lstLayers.Items.Add(item);
            }

            // Build dropdown list data
            for (var i = 0; i < 5; i++)
            {
                this.cmbList.Items.Add(new DarkDropdownItem($"Dropdown item #{i}"));
            }
        }
    }
}