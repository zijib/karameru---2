# Système de Localisation - Karameru2

## Vue d'ensemble

Karameru2 supporte la localisation complète de son interface utilisateur en 7 langues :
- Français (fr) - Langue par défaut
- Anglais (en)
- Espagnol (es)
- Italien (it)
- Japonais (ja)
- Coréen (ko)
- Chinois (zh)

## Structure des fichiers

Les fichiers de ressources se trouvent dans `Karameru2/Resources/` :
- `Lang.fr.resx` - Français
- `Lang.en.resx` - Anglais
- `Lang.es.resx` - Espagnol
- `Lang.it.resx` - Italien
- `Lang.ja.resx` - Japonais
- `Lang.ko.resx` - Coréen
- `Lang.zh.resx` - Chinois

## Clés de localisation disponibles

### Interface principale
- `AppTitle` - Titre de l'application
- `MenuLanguage` - Menu Langue
- `MenuFile` - Menu Fichier
- `MenuHelp` - Menu Aide
- `MenuFormats` - Menu Formats supportés

### Boutons et actions
- `OpenArchiveButton` - Bouton "Ouvrir une archive..."
- `ViewBinaryButton` - Bouton "Voir le binaire"
- `RunButton` - Bouton "Lancer"

### Labels et textes
- `PromptLabel` - Label pour la zone de saisie
- `ResultLabel` - Label pour la zone de résultat
- `Model3DLabel` - Label pour l'affichage 3D

### Messages d'erreur
- `UnsupportedFormatError` - Erreur de format non supporté
- `ErrorTitle` - Titre des boîtes de dialogue d'erreur
- `BinaryNotSupportedError` - Erreur d'affichage binaire non supporté

### Fenêtres et dialogues
- `BinaryWindowTitle` - Titre de la fenêtre binaire (avec paramètre {0})
- `SupportedFormatsTitle` - Titre de la boîte de dialogue des formats

## Comment ajouter une nouvelle langue

1. Créer un nouveau fichier `Lang.XX.resx` dans le dossier `Resources/`
2. Copier la structure du fichier `Lang.en.resx`
3. Traduire toutes les valeurs `<value>` dans la nouvelle langue
4. Ajouter la nouvelle langue dans le ComboBox de `MainForm.cs`

## Comment ajouter de nouveaux textes

1. Ajouter la nouvelle clé dans tous les fichiers `.resx`
2. Utiliser `_res.GetString("NouvelleCle") ?? "Texte par défaut"` dans le code
3. Pour les textes avec paramètres, utiliser `string.Format(_res.GetString("Cle"), param1, param2)`

## Utilisation dans le code

```csharp
// Texte simple
this.Text = _res.GetString("AppTitle") ?? "Karameru2";

// Texte avec paramètres
var title = string.Format(_res.GetString("BinaryWindowTitle") ?? "Binaire de {0}", fileName);

// Message d'erreur
MessageBox.Show(_res.GetString("ErrorMessage") ?? "Erreur", 
                _res.GetString("ErrorTitle") ?? "Erreur", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
```

## Changement de langue

L'utilisateur peut changer de langue via le menu "Langue" dans l'interface. Le changement est immédiat et l'interface se reconstruit automatiquement.

## Notes techniques

- Le système utilise `ResourceManager` de .NET
- La culture est définie avec `Thread.CurrentThread.CurrentUICulture`
- Les ressources sont chargées depuis l'assembly principal
- Le fallback vers le français est géré avec l'opérateur `??` 