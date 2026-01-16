using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Resources;
using System.Collections.Generic;

namespace Karameru2
{
    public partial class MainForm : Form
    {
        private ResourceManager _res;
        private ComboBox _langCombo;
        private TreeView _fileTree;
        private PictureBox _imageBox;
        private TextBox _promptBox;
        private Button _runButton;
        private TextBox _resultBox;
        private Label _model3dLabel;

        private string? lastArchivePath = null;

        // Cache de l’arborescence
        private Karameru2.Plugins.Arc0Reader.ArchiveNode? _cachedRoot = null;

        public MainForm()
        {
            InitLang();
            BuildUI();
        }

        private void InitLang()
        {
            SetLang("fr");
        }

        private void SetLang(string lang)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);
            _res = new ResourceManager("Karameru2.Resources.Lang", typeof(MainForm).Assembly);
        }

        private void BuildUI()
        {
            this.Text = _res.GetString("AppTitle") ?? "Karameru2";
            this.Size = new Size(900, 600);

            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem(_res.GetString("MenuFile") ?? "Fichier");
            var langMenu = new ToolStripMenuItem(_res.GetString("MenuLanguage") ?? "Langue");
            var helpMenu = new ToolStripMenuItem(_res.GetString("MenuHelp") ?? "Aide");
            var formatsMenu = new ToolStripMenuItem(_res.GetString("MenuFormats") ?? "Formats supportés");

            formatsMenu.Click += (s, e) =>
            {
                var formats = Karameru2.Plugins.PluginManager.ArchivePlugins;
                string msg = string.Join("\r\n", formats.ConvertAll(f => $"{f.Name} ({string.Join(", ", f.FileExtensions)}) : {f.Description}"));
                MessageBox.Show(msg, "Formats supportés", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            _langCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            _langCombo.Items.AddRange(new object[] {
                new { Code = "fr", Name = "Français" },
                new { Code = "en", Name = "English" },
                new { Code = "es", Name = "Español" },
                new { Code = "it", Name = "Italiano" },
                new { Code = "ja", Name = "日本語" },
                new { Code = "ko", Name = "한국어" },
                new { Code = "zh", Name = "中文" }
            });
            _langCombo.DisplayMember = "Name";
            _langCombo.ValueMember = "Code";
            _langCombo.SelectedIndex = 0;

            _langCombo.SelectedIndexChanged += (s, e) =>
            {
                var selectedLang = _langCombo.SelectedItem.GetType().GetProperty("Code")?.GetValue(_langCombo.SelectedItem)?.ToString() ?? "fr";
                SetLang(selectedLang);
                Controls.Clear();
                BuildUI();
            };

            langMenu.DropDownItems.Add(new ToolStripControlHost(_langCombo));

            menu.Items.Add(fileMenu);
            menu.Items.Add(langMenu);
            menu.Items.Add(helpMenu);
            menu.Items.Add(formatsMenu);
            Controls.Add(menu);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 250 };
            Controls.Add(split);
            split.BringToFront();

            var openArchiveBtn = new Button { Text = "Ouvrir une archive...", Dock = DockStyle.Top, Height = 40 };
            openArchiveBtn.Click += (s, e) => OpenArchive();
            split.Panel1.Controls.Add(openArchiveBtn);

            _fileTree = new TreeView { Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(_fileTree);

            _fileTree.BeforeExpand += (s, e) =>
            {
                if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "...")
                    AddChildrenLazy(e.Node);
            };

            _fileTree.AfterSelect += FileTree_AfterSelect;

            var panel = new Panel { Dock = DockStyle.Fill };
            split.Panel2.Controls.Add(panel);

            _imageBox = new PictureBox { Dock = DockStyle.Top, Height = 250, SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
            panel.Controls.Add(_imageBox);

            _model3dLabel = new Label { Dock = DockStyle.Top, Height = 30, Text = "[Affichage 3D à venir]", TextAlign = ContentAlignment.MiddleCenter, Visible = false };
            panel.Controls.Add(_model3dLabel);

            _promptBox = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Votre question ou recherche :" };
            _runButton = new Button { Dock = DockStyle.Top, Text = "Lancer" };
            _resultBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

            _runButton.Click += (s, e) =>
            {
                _resultBox.Text = $"Prompt : {_promptBox.Text}\r\n(Résultat simulé)";
            };

            panel.Controls.Add(_resultBox);
            panel.Controls.Add(_runButton);
            panel.Controls.Add(_promptBox);
        }

        private void OpenArchive()
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Archives Level-5 (*.fa)|*.fa|Tous les fichiers (*.*)|*.*";

            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            lastArchivePath = ofd.FileName;

            var plugin = Karameru2.Plugins.PluginManager.ArchivePlugins.Find(p => p.IsMatch(lastArchivePath));
            if (plugin is not Karameru2.Plugins.Level5FaPlugin faPlugin)
            {
                MessageBox.Show("Format non supporté.");
                return;
            }

            _cachedRoot = faPlugin.GetArchiveTree(lastArchivePath);

            _fileTree.BeginUpdate();
            _fileTree.Nodes.Clear();

            foreach (var child in _cachedRoot.Children)
                _fileTree.Nodes.Add(CreateLazyNode(child));

            _fileTree.EndUpdate();
        }

        private TreeNode CreateLazyNode(Karameru2.Plugins.Arc0Reader.ArchiveNode node)
        {
            var t = new TreeNode(node.Name) { Tag = node };

            if (node.IsDirectory)
                t.Nodes.Add(new TreeNode("..."));

            return t;
        }

        private void AddChildrenLazy(TreeNode parent)
        {
            var node = parent.Tag as Karameru2.Plugins.Arc0Reader.ArchiveNode;
            if (node == null) return;

            parent.Nodes.Clear();

            foreach (var child in node.Children)
                parent.Nodes.Add(CreateLazyNode(child));
        }

        private void FileTree_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node == null || lastArchivePath == null)
                return;

            var arcNode = e.Node.Tag as Karameru2.Plugins.Arc0Reader.ArchiveNode;
            if (arcNode == null || arcNode.IsDirectory)
            {
                _imageBox.Visible = false;
                _model3dLabel.Visible = false;
                _resultBox.Visible = false;
                return;
            }

            var plugin = Karameru2.Plugins.PluginManager.ArchivePlugins.Find(p => p.IsMatch(lastArchivePath));
            if (plugin is not Karameru2.Plugins.Level5FaPlugin faPlugin)
                return;

            string internalPath = GetFullPath(e.Node);

            byte[] data;
            try
            {
                data = faPlugin.ExtractFile(lastArchivePath, internalPath);
            }
            catch
            {
                return;
            }

            var ext = Path.GetExtension(internalPath).ToLowerInvariant();

            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")
            {
                try
                {
                    using var ms = new MemoryStream(data);
                    _imageBox.Image = Image.FromStream(ms);
                    _imageBox.Visible = true;
                }
                catch
                {
                    _imageBox.Visible = false;
                }

                _model3dLabel.Visible = false;
                _resultBox.Visible = false;
            }
            else if (ext is ".txt" or ".msg" or ".bin" or ".dat")
            {
                _resultBox.Text = System.Text.Encoding.UTF8.GetString(data, 0, Math.Min(4096, data.Length));
                _resultBox.Visible = true;
                _imageBox.Visible = false;
                _model3dLabel.Visible = false;
            }
            else if (ext is ".bmd" or ".bch" or ".bcmdl")
            {
                _model3dLabel.Visible = true;
                _imageBox.Visible = false;
                _resultBox.Visible = false;
            }
            else
            {
                _imageBox.Visible = false;
                _model3dLabel.Visible = false;
                _resultBox.Visible = false;
            }
        }

        private string GetFullPath(TreeNode node)
        {
            var parts = new List<string>();
            var n = node;

            while (n != null && n.Parent != null)
            {
                parts.Insert(0, n.Text);
                n = n.Parent;
            }

            return string.Join("/", parts);
        }
    }
}
