using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Resources;
using System.Collections.Generic; // Added for List

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
        private string lastArchivePath = null;

        public MainForm()
        {
            // InitializeComponent(); // Supprimé car inutile
            InitLang();
            BuildUI();
        }

        private void InitLang()
        {
            // Par défaut : français
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

            // Menu
            var menu = new MenuStrip();
            var fileMenu = new ToolStripMenuItem(_res.GetString("MenuFile") ?? "Fichier");
            var langMenu = new ToolStripMenuItem(_res.GetString("MenuLanguage") ?? "Langue");
            var helpMenu = new ToolStripMenuItem(_res.GetString("MenuHelp") ?? "Aide");
            var formatsMenu = new ToolStripMenuItem(_res.GetString("MenuFormats") ?? "Formats supportés");
            formatsMenu.Click += (s, e) =>
            {
                var formats = Karameru2.Plugins.PluginManager.ArchivePlugins;
                string msg = string.Join("\r\n", formats.ConvertAll(f => $"{f.Name} ({string.Join(", ", f.FileExtensions)}) : {f.Description}"));
                MessageBox.Show(msg, _res.GetString("SupportedFormatsTitle") ?? "Formats d'archives supportés", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // Langue : ComboBox dans le menu
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
                if (_langCombo.SelectedItem != null)
                {
                    var selectedLang = _langCombo.SelectedItem.GetType().GetProperty("Code")?.GetValue(_langCombo.SelectedItem)?.ToString() ?? "fr";
                    SetLang(selectedLang);
                    Controls.Clear();
                    BuildUI();
                }
            };
            var host = new ToolStripControlHost(_langCombo);
            langMenu.DropDownItems.Add(host);

            menu.Items.Add(fileMenu);
            menu.Items.Add(langMenu);
            menu.Items.Add(helpMenu);
            menu.Items.Add(formatsMenu);
            this.MainMenuStrip = menu;
            Controls.Add(menu);

            // Layout principal
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 250 };
            Controls.Add(split);
            split.BringToFront();

            // Panel gauche : explorateur d'archives
            var openArchiveBtn = new Button { Text = _res.GetString("OpenArchiveButton") ?? "Ouvrir une archive...", Dock = DockStyle.Top, Height = 40 };
            openArchiveBtn.Click += (s, e) => OpenArchive();
            split.Panel1.Controls.Add(openArchiveBtn);

            _fileTree = new TreeView { Dock = DockStyle.Top, Height = 250 };
            split.Panel1.Controls.Add(_fileTree);

            var viewHexBtn = new Button { Text = _res.GetString("ViewBinaryButton") ?? "Voir le binaire", Dock = DockStyle.Top, Height = 32, Enabled = false };
            split.Panel1.Controls.Add(viewHexBtn);

            viewHexBtn.Click += (s, e) =>
            {
                if (_fileTree.SelectedNode == null || lastArchivePath == null) return;
                string fileName = GetFullPath(_fileTree.SelectedNode);
                ShowHexWindow(lastArchivePath, fileName);
            };
            // On ne charge plus les disques par défaut
            //_fileTree.AfterSelect += FileTree_AfterSelect;
            //LoadDrives();

            // Panel droit : contenu dynamique
            var panel = new Panel { Dock = DockStyle.Fill };
            split.Panel2.Controls.Add(panel);

            _imageBox = new PictureBox { Dock = DockStyle.Top, Height = 250, SizeMode = PictureBoxSizeMode.Zoom, Visible = false };
            panel.Controls.Add(_imageBox);

            _model3dLabel = new Label { Dock = DockStyle.Top, Height = 30, Text = _res.GetString("Model3DLabel") ?? "[Affichage 3D à venir]", TextAlign = ContentAlignment.MiddleCenter, Visible = false };
            panel.Controls.Add(_model3dLabel);

            _promptBox = new TextBox { Dock = DockStyle.Top, PlaceholderText = _res.GetString("PromptLabel") ?? "Votre question ou recherche :" };
            _runButton = new Button { Dock = DockStyle.Top, Text = _res.GetString("RunButton") ?? "Lancer" };
            _resultBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            _runButton.Click += (s, e) =>
            {
                _resultBox.Text = $"Prompt : {_promptBox.Text}\r\n(Résultat simulé)";
            };
            panel.Controls.Add(_resultBox);
            panel.Controls.Add(_runButton);
            panel.Controls.Add(_promptBox);

            // Affichage intelligent selon le type de fichier sélectionné
            _fileTree.AfterSelect += (s, e) =>
            {
                if (e.Node == null || e.Node.Nodes.Count != 0 || lastArchivePath == null) {
                    _imageBox.Visible = false;
                    _model3dLabel.Visible = false;
                    _resultBox.Visible = false;
                    return;
                }
                string fileName = GetFullPath(e.Node);
                var ext = System.IO.Path.GetExtension(fileName).ToLower();
                var plugin = Karameru2.Plugins.PluginManager.ArchivePlugins.Find(p => p.IsMatch(lastArchivePath));
                if (plugin is Karameru2.Plugins.Level5FaPlugin faPlugin)
                {
                    byte[] data;
                    try
                    {
                        data = faPlugin.ExtractFile(lastArchivePath, fileName);
                    }
                    catch
                    {
                        _imageBox.Visible = false;
                        _model3dLabel.Visible = false;
                        _resultBox.Visible = false;
                        return;
                    }

                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                    {
                        try
                        {
                            using var ms = new MemoryStream(data);
                            _imageBox.Image = Image.FromStream(ms);
                            _imageBox.Visible = true;
                        }
                        catch { _imageBox.Visible = false; }
                        _model3dLabel.Visible = false;
                        _resultBox.Visible = false;
                    }
                    else if (ext == ".txt" || ext == ".msg" || ext == ".bin" || ext == ".dat")
                    {
                        // Affichage texte lisible (limite 4 Ko)
                        _resultBox.Text = System.Text.Encoding.UTF8.GetString(data, 0, Math.Min(4096, data.Length));
                        _resultBox.Visible = true;
                        _imageBox.Visible = false;
                        _model3dLabel.Visible = false;
                    }
                    else if (ext == ".bmd" || ext == ".bch" || ext == ".bcmdl")
                    {
                        _model3dLabel.Visible = true;
                        _imageBox.Visible = false;
                        _resultBox.Visible = false;
                    }
                    else
                    {
                        // Par défaut, rien
                        _imageBox.Visible = false;
                        _model3dLabel.Visible = false;
                        _resultBox.Visible = false;
                    }
                }
            };
        }

        private void LoadDrives()
        {
            _fileTree.Nodes.Clear();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var node = new TreeNode(drive.Name) { Tag = drive.Name };
                node.Nodes.Add(""); // Dummy
                _fileTree.Nodes.Add(node);
            }
            _fileTree.BeforeExpand += (s, e) =>
            {
                if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "")
                {
                    e.Node.Nodes.Clear();
                    try
                    {
                        foreach (var dir in Directory.GetDirectories(e.Node.Tag.ToString()))
                        {
                            var n = new TreeNode(Path.GetFileName(dir)) { Tag = dir };
                            n.Nodes.Add("");
                            e.Node.Nodes.Add(n);
                        }
                        foreach (var file in Directory.GetFiles(e.Node.Tag.ToString()))
                        {
                            var n = new TreeNode(Path.GetFileName(file)) { Tag = file };
                            e.Node.Nodes.Add(n);
                        }
                    }
                    catch { }
                }
            };
        }

        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var path = e.Node.Tag.ToString();
            if (File.Exists(path))
            {
                var ext = Path.GetExtension(path).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                {
                    _imageBox.Image = Image.FromFile(path);
                    _imageBox.Visible = true;
                    _model3dLabel.Visible = false;
                }
                else if (ext == ".obj" || ext == ".fbx" || ext == ".dae" || ext == ".3ds")
                {
                    _imageBox.Visible = false;
                    _model3dLabel.Visible = true;
                }
                else
                {
                    _imageBox.Visible = false;
                    _model3dLabel.Visible = false;
                }
            }
            else
            {
                _imageBox.Visible = false;
                _model3dLabel.Visible = false;
            }
        }

        private void OpenArchive()
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Archives supportées (*.fa;*.arc)|*.fa;*.arc|Tous les fichiers (*.*)|*.*";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var file = ofd.FileName;
                lastArchivePath = file;
                var plugin = Karameru2.Plugins.PluginManager.ArchivePlugins.Find(p => p.IsMatch(file));
                if (plugin == null)
                {
                    MessageBox.Show(_res.GetString("UnsupportedFormatError") ?? "Format non supporté.", _res.GetString("ErrorTitle") ?? "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                _fileTree.Nodes.Clear();
                if (plugin is Karameru2.Plugins.Level5FaPlugin faPlugin)
                {
                    var rootNode = faPlugin.GetArchiveTree(file);
                    _fileTree.Nodes.Clear();
                    foreach (var dir in rootNode.Children)
                    {
                        var dirTreeNode = new TreeNode(dir.Name);
                        AddChildrenRecursive(dirTreeNode, dir);
                        _fileTree.Nodes.Add(dirTreeNode);
                    }
                }
                else
                {
                    // Simulation pour les autres plugins
                    _fileTree.Nodes.Add(new TreeNode("file1.bin"));
                    _fileTree.Nodes.Add(new TreeNode("file2.bin"));
                }
                // (plus aucune référence à 'root' ici)
            }
        }

        private string GetFullPath(TreeNode node)
        {
            var parts = new List<string>();
            var n = node;
            while (n != null && n.Parent != null) // On ne veut pas le nom de l'archive
            {
                parts.Insert(0, n.Text);
                n = n.Parent;
            }
            return string.Join("/", parts);
        }

        private void ShowHexWindow(string archivePath, string internalPath)
        {
            // Version simplifiée : recherche du fichier dans l'archive par nom, puis affichage hexadécimal
            var plugin = Karameru2.Plugins.PluginManager.ArchivePlugins.Find(p => p.IsMatch(archivePath));
            if (plugin is Karameru2.Plugins.Level5FaPlugin faPlugin)
            {
                // Simulation : on relit l'archive et on cherche le nom du fichier, puis on affiche un extrait binaire
                using var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);
                var data = br.ReadBytes((int)fs.Length);
                var text = System.Text.Encoding.GetEncoding("shift-jis").GetString(data);
                var idx = text.IndexOf(internalPath, StringComparison.OrdinalIgnoreCase);
                int offset = idx >= 0 ? idx : 0;
                int len = Math.Min(256, data.Length - offset);
                var hex = BitConverter.ToString(data, offset, len).Replace("-", " ");
                var ascii = System.Text.Encoding.ASCII.GetString(data, offset, len);
                var form = new Form { Text = string.Format(_res.GetString("BinaryWindowTitle") ?? "Binaire de {0}", internalPath), Width = 600, Height = 400 };
                var box = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, Font = new System.Drawing.Font("Consolas", 10) };
                box.Text = $"Offset: 0x{offset:X}\r\n\r\nHEX:\r\n{hex}\r\n\r\nASCII:\r\n{ascii}";
                form.Controls.Add(box);
                form.ShowDialog();
            }
            else
            {
                MessageBox.Show(_res.GetString("BinaryNotSupportedError") ?? "Affichage binaire non supporté pour ce format.");
            }
        }

        private void AddChildrenRecursive(TreeNode parent, Karameru2.Plugins.Arc0Reader.ArchiveNode node)
        {
            foreach (var child in node.Children)
            {
                var t = new TreeNode(child.Name);
                parent.Nodes.Add(t);
                if (child.IsDirectory)
                    AddChildrenRecursive(t, child);
            }
        }
    }
} 