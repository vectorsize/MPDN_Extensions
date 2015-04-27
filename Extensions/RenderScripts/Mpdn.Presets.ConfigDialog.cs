// This file is a part of MPDN Extensions.
// https://github.com/zachsaw/MPDN_Extensions
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library.
// 
using System;
using System.Drawing;
//using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Mpdn.RenderScript;
using Mpdn.Config;

namespace Mpdn.RenderScript
{
    namespace Mpdn.Presets
    {
        public partial class PresetDialog : PresetDialogBase
        {
            private const string SELECTED_INDICATOR_STR = "➔";

            private int m_SelectedIndex = -1;
            protected int SelectedIndex
            {
                get { return m_SelectedIndex;  }
                set 
                {
                    if (0 <= value && value < listViewChain.Items.Count)
                    {
                        m_SelectedIndex = value;
                        foreach (ListViewItem item in listViewChain.SelectedItems)
                            item.Selected = false;
                        listViewChain.Items[value].Selected = true;
                    }
                    else
                        m_SelectedIndex = -1;
                }
            }

            public PresetDialog()
            {
                InitializeComponent();

                var renderScripts = PlayerControl.RenderScripts
                    .Where(script => script is IRenderChainUi)
                    .Select(x => (x as IRenderChainUi).CreateNew())
                    .Concat(new [] { RenderChainUi.Identity } )
                    .OrderBy(x => x.Category + SELECTED_INDICATOR_STR + x.Descriptor.Name);

                var groups = new Dictionary<string, ListViewGroup>();
                foreach (var script in renderScripts)
                if (script.Category.ToLowerInvariant() != "hidden")
                {
                    var item = listViewAvail.Items.Add(string.Empty);
                    item.SubItems.Add(script.Descriptor.Name);
                    item.SubItems.Add(script.Descriptor.Description);
                    item.Tag = script;

                    if (!groups.ContainsKey(script.Category))
                        groups.Add(
                            script.Category, 
                            listViewAvail.Groups.Add("", script.Category));

                    item.Group = groups[script.Category];
                }

                if (listViewAvail.Items.Count > 0)
                {
                    var firstItem = listViewAvail.Items[0];
                    firstItem.Text = SELECTED_INDICATOR_STR;
                    firstItem.Selected = true;
                }

                listViewAvail.Sort();

                ResizeLists();
                UpdateButtons();
            }

            protected IList<Preset> GatherPresets(ListView.ListViewItemCollection items)
            {
                var scripts = from item in listViewChain.Items.Cast<ListViewItem>()
                              select (Preset)item.Tag;
                return scripts.ToList();
            }

            protected IList<Preset> GatherPresets(ListViewGroupCollection items)
            {
                var scripts = from item in listViewChain.Items.Cast<ListViewItem>()
                              select (Preset)item.Tag;
                return scripts.ToList();
            }

            protected override void LoadSettings()
            {
                AddScripts(Settings.Options);

                ResizeLists();
                UpdateButtons();
            }

            protected override void SaveSettings()
            {
                Settings.Options = GatherPresets(listViewChain.Items);
            }

            private void AddScripts(IEnumerable<Preset> presets, int index = -1)
            {
                foreach (var preset in presets)
                {
                    ListViewItem item;
                    if (index < 0)
                        item = listViewChain.Items.Add(string.Empty);
                    else
                        item = listViewChain.Items.Insert(index++,string.Empty);
                    item.SubItems.Add(preset.Name);
                    item.SubItems.Add(preset.Description);
                    item.Tag = preset;
                }
                ResizeLists();
            }

            private void RemoveScript(ListViewItem selectedItem)
            {
                var preset = (Preset)selectedItem.Tag;
                preset.Destroy();

                var index = selectedItem.Index;
                selectedItem.Remove();
                if (index < listViewChain.Items.Count)
                {
                    listViewChain.Items[index].Selected = true;
                }
                else if (listViewChain.Items.Count > 0)
                {
                    listViewChain.Items[listViewChain.Items.Count - 1].Selected = true;
                }

                ResizeLists();
                UpdateButtons();
            }

            private void ListViewSelectedIndexChanged(object sender, EventArgs e)
            {
                foreach (ListViewItem i in listViewAvail.Items)
                {
                    i.Text = string.Empty;
                }

                UpdateButtons();

                if (listViewAvail.SelectedItems.Count <= 0)
                    return;

                var item = listViewAvail.SelectedItems[0];

                item.Text = SELECTED_INDICATOR_STR;

                var script = (IRenderChainUi) item.Tag;
                labelCopyright.Text = script == null ? string.Empty : script.Descriptor.Copyright;
            }

            private void UpdateButtons()
            {
                buttonAdd.Enabled = listViewAvail.SelectedItems.Count > 0;
                buttonMinus.Enabled = listViewChain.SelectedItems.Count > 0;
                buttonClear.Enabled = listViewChain.Items.Count > 0;
                buttonUp.Enabled = listViewChain.SelectedItems.Count > 0 && listViewChain.SelectedItems[0].Index > 0;
                buttonDown.Enabled = listViewChain.SelectedItems.Count > 0 &&
                                     listViewChain.SelectedItems[listViewChain.SelectedItems.Count - 1].Index < listViewChain.Items.Count - 1;
                buttonConfigure.Enabled = buttonMinus.Enabled && (listViewChain.SelectedItems[0].Tag as Preset).HasConfigDialog();

                menuAdd.Enabled = buttonAdd.Enabled;
                menuRemove.Enabled = buttonMinus.Enabled;
                menuClear.Enabled = buttonClear.Enabled;
                menuConfigure.Enabled = buttonConfigure.Enabled;
            }

            private void ListViewChainSelectedIndexChanged(object sender, EventArgs e)
            {
                foreach (ListViewItem i in listViewChain.Items)
                {
                    i.Text = string.Empty;
                }

                if (listViewChain.SelectedItems.Count > 0)
                {
                    var item = listViewChain.SelectedItems[0];
                    m_SelectedIndex = item.Index;
                    item.Text = SELECTED_INDICATOR_STR;

                    var preset = (Preset)item.Tag;
                    NameBox.Text = preset.Name;
                }
                else
                    NameBox.Text = string.Empty;

                UpdateButtons();
            }

            private void presetTextChanged(object sender, EventArgs e)
            {
                if (listViewChain.SelectedItems.Count == 1)
                {
                    var item = listViewChain.SelectedItems[0];
                    Preset preset = (Preset)item.Tag;

                    preset.Name = NameBox.Text;
                    UpdateItemText(item, preset);
                }
            }

            private void ButtonConfigureClick(object sender, EventArgs e)
            {
                if (listViewChain.SelectedItems.Count <= 0)
                    return;

                var item = listViewChain.SelectedItems[0];
                var preset = (Preset)item.Tag;
                if (preset.HasConfigDialog() && preset.ShowConfigDialog(Owner))
                    UpdateItemText(item, preset);
            }

            private void ButtonAddClick(object sender, EventArgs e)
            {
                foreach (ListViewItem item in listViewAvail.SelectedItems) 
                    AddScript(item);
            }

            private void ButtonMinusClick(object sender, EventArgs e)
            {
                foreach (ListViewItem item in listViewChain.SelectedItems)
                    RemoveScript(item);
            }

            private void ButtonClearClick(object sender, EventArgs e)
            {
                while (listViewChain.Items.Count > 0)
                {
                    RemoveScript(listViewChain.Items[0]);
                }
                UpdateButtons();
            }

            private void ButtonUpClick(object sender, EventArgs e)
            {
                MoveListViewItems(listViewChain, MoveDirection.Up);
                UpdateButtons();
            }

            private void ButtonDownClick(object sender, EventArgs e)
            {
                MoveListViewItems(listViewChain, MoveDirection.Down);
                UpdateButtons();
            }

            private void AddScript(ListViewItem selectedItem)
            {
                var item = (ListViewItem) selectedItem.Clone();
                item.Text = string.Empty;

                var scriptRenderer = (IRenderChainUi) item.Tag;

                var preset = scriptRenderer.MakeNewPreset();

                item.Tag = preset;
                UpdateItemText(item, preset);
                listViewChain.Items.Add(item);

                ResizeLists();
                UpdateButtons();
            }

            private void UpdateItemText(ListViewItem item, Preset preset)
            {
                item.SubItems[1].Text = preset.Name;
                item.SubItems[2].Text = preset.Description;

                ResizeLists();
            }

            private static void MoveListViewItems(ListView listView, MoveDirection direction)
            {
                var valid = listView.SelectedItems.Count > 0 &&
                            ((direction == MoveDirection.Down &&
                              (listView.SelectedItems[listView.SelectedItems.Count - 1].Index < listView.Items.Count - 1))
                             || (direction == MoveDirection.Up && (listView.SelectedItems[0].Index > 0)));

                if (!valid)
                    return;

                var start = true;
                var firstIdx = 0;
                var items = new List<ListViewItem>();

                foreach (ListViewItem i in listView.SelectedItems)
                {
                    if (start)
                    {
                        firstIdx = i.Index;
                        start = false;
                    }
                    items.Add(i);
                }

                listView.BeginUpdate();

                foreach (ListViewItem i in listView.SelectedItems)
                {
                    i.Remove();
                }

                if (direction == MoveDirection.Up)
                {
                    var insertTo = firstIdx - 1;
                    foreach (var i in items)
                    {
                        i.Selected = true;
                        listView.Items.Insert(insertTo, i);
                        insertTo++;
                    }
                }
                else
                {
                    var insertTo = firstIdx + 1;
                    foreach (var i in items)
                    {
                        i.Selected = true;
                        listView.Items.Insert(insertTo, i);
                        insertTo++;
                    }
                }

                listView.EndUpdate();
                listView.Focus();
            }

            private void list_ItemCopyDrag(object sender, ItemDragEventArgs e)
            {
                DoDragDrop((sender as ListView).SelectedItems, DragDropEffects.Copy);
            }

            private void list_ItemMoveDrag(object sender, ItemDragEventArgs e)
            {
                DoDragDrop((sender as ListView).SelectedItems, DragDropEffects.Move);
            }

            private void list_DragEnter(object sender, DragEventArgs e)
            {
                e.Effect = e.AllowedEffect;
            }

            private void list_DragDrop(object sender, DragEventArgs e)
            {
                Point cp = listViewChain.PointToClient(new Point(e.X, e.Y));
                ListViewItem dragToItem = listViewChain.GetItemAt(cp.X, cp.Y);
                bool after = (dragToItem != null) && listViewChain.GetItemRect(dragToItem.Index).Bottom - 8 <= cp.Y;
                var draggedItems = e.Data.GetData(typeof(ListView.SelectedListViewItemCollection)) as ListView.SelectedListViewItemCollection;
                if (draggedItems == null || draggedItems.Count == 0)
                    return;                

                if (e.Effect == DragDropEffects.Copy)
                {
                    var items = draggedItems.Cast<ListViewItem>();
                    var index = dragToItem == null ? listViewChain.Items.Count : dragToItem.Index + (after ? 1 : 0);

                    AddScripts(items.Select(item => (item.Tag as IRenderChainUi).MakeNewPreset()), index);
                }
                else if (e.Effect == DragDropEffects.Move)
                {
                    if (draggedItems.Contains(dragToItem))
                        return;

                    var items = new List<ListViewItem>();
                    foreach (ListViewItem item in draggedItems.Cast<ListViewItem>())
                    {
                        item.Remove();
                        items.Add(item);
                    }

                    var index = dragToItem == null ? listViewChain.Items.Count : dragToItem.Index + (after ? 1 : 0);
                    foreach (ListViewItem item in items)
                    {
                        listViewChain.Items.Insert(index, item);
                        index++;
                    }
                }
            }

            private void list_DragDropRemove(object sender, DragEventArgs e)
            {
                var draggedItems = e.Data.GetData(typeof(ListView.SelectedListViewItemCollection)) as ListView.SelectedListViewItemCollection;
                if (draggedItems == null)
                    return;

                if (e.Effect == DragDropEffects.Move)
                {
                    var items = new List<ListViewItem>();
                    foreach (ListViewItem item in draggedItems.Cast<ListViewItem>())
                        RemoveScript(item);
                }
            }

            private enum MoveDirection
            {
                Up = -1,
                Down = 1
            };

            private void SelectAll(object sender, EventArgs e)
            {
                foreach (ListViewItem item in listViewChain.Items)
                    item.Selected = true;
            }

            private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
            {
                ResizeLists();
            }

            private void ResizeLists()
            {
                listViewChain.BeginUpdate(); 
                {
                    listViewChain.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                    listViewChain.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                }
                listViewChain.EndUpdate();

                listViewAvail.BeginUpdate();
                {
                    listViewAvail.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                    listViewAvail.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                }
                listViewAvail.EndUpdate();
            }

            private void PresetDialog_ResizeEnd(object sender, EventArgs e)
            {
                ResizeLists();
            }

            private void NameBox_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.IsInputKey = true;
                }
            }

            private void NameBox_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    SelectedIndex++;
                    if (SelectedIndex != -1)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        NameBox.SelectAll();
                    }
                    else
                        AcceptButton.PerformClick();
                }
                else if (e.KeyCode == Keys.Down)
                {
                    SelectedIndex++;
                    if (SelectedIndex == -1 && listViewChain.Items.Count > 0)
                        SelectedIndex = 0;

                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    NameBox.SelectAll();
                }
                else if (e.KeyCode == Keys.Up)
                {
                    SelectedIndex--;
                    if (SelectedIndex == -1)
                        SelectedIndex = listViewChain.Items.Count - 1;

                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    NameBox.SelectAll();
                }
            }


        }

        public class PresetDialogBase : ScriptConfigDialog<MultiPreset>
        {
        }
    }
}
