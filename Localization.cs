using System.Collections.Generic;

namespace DDS_Convert;

public enum AppLanguage
{
    English,
    PortugueseBr,
    German,
    French,
    Chinese,
}

/// <summary>
/// Simple in-memory translation table. Deliberately scoped to interface chrome only - Asset Type
/// values, Status values (Pending/Done/Failed/...) and Activity Log messages stay in English on
/// purpose: those are compared literally elsewhere in the code (e.g. CollectPendingItems checks
/// for the literal string "Done") and are diagnostic/technical text modders often paste into
/// support requests, so keeping them language-independent avoids both correctness bugs and
/// confusing "which word did the game actually show me" mismatches.
/// </summary>
public static class Loc
{
    public static AppLanguage Current = AppLanguage.English;

    public static string Code(AppLanguage lang) => lang switch
    {
        AppLanguage.PortugueseBr => "pt-BR",
        AppLanguage.German => "de",
        AppLanguage.French => "fr",
        AppLanguage.Chinese => "zh",
        _ => "en",
    };

    public static AppLanguage FromCode(string? code) => code switch
    {
        "pt-BR" => AppLanguage.PortugueseBr,
        "de" => AppLanguage.German,
        "fr" => AppLanguage.French,
        "zh" => AppLanguage.Chinese,
        _ => AppLanguage.English,
    };

    /// <summary>Each language's name for itself - always shown the same way regardless of the current UI language.</summary>
    public static string NativeName(AppLanguage lang) => lang switch
    {
        AppLanguage.PortugueseBr => "Português (Brasil)",
        AppLanguage.German => "Deutsch",
        AppLanguage.French => "Français",
        AppLanguage.Chinese => "中文",
        _ => "English",
    };

    public static string T(string key)
    {
        if (!Table.TryGetValue(key, out var variants)) return key;
        var value = variants[(int)Current];
        return string.IsNullOrEmpty(value) ? variants[0] : value;
    }

    public static string F(string key, params object[] args) => string.Format(T(key), args);

    // Column order per language: English, Portuguese (Brazil), German, French, Chinese.
    static readonly Dictionary<string, string[]> Table = new()
    {
        // ---- Menu -----------------------------------------------------------------
        ["menu.file"] = new[] { "&File", "&Arquivo", "&Datei", "&Fichier", "文件(&F)" },
        ["menu.file.addFiles"] = new[] { "Add Files…", "Adicionar Arquivos…", "Dateien hinzufügen…", "Ajouter des fichiers…", "添加文件…" },
        ["menu.file.openAssets"] = new[] { "Open Output Folder (Assets)", "Abrir Pasta de Saída (Assets)", "Ausgabeordner öffnen (Assets)", "Ouvrir le dossier de sortie (Assets)", "打开输出文件夹 (Assets)" },
        ["menu.file.openLowRes"] = new[] { "Open Output Folder (AssetsLowRes)", "Abrir Pasta de Saída (AssetsLowRes)", "Ausgabeordner öffnen (AssetsLowRes)", "Ouvrir le dossier de sortie (AssetsLowRes)", "打开输出文件夹 (AssetsLowRes)" },
        ["menu.file.exit"] = new[] { "E&xit", "S&air", "&Beenden", "&Quitter", "退出(&X)" },
        ["menu.help"] = new[] { "&Help", "Aj&uda", "&Hilfe", "&Aide", "帮助(&H)" },
        ["menu.help.howToUse"] = new[] { "How to Use…", "Como Usar…", "Bedienungsanleitung…", "Comment utiliser…", "使用说明…" },
        ["menu.help.donate"] = new[] { "Donate…", "Doar…", "Spenden…", "Faire un don…", "捐赠…" },
        ["menu.help.about"] = new[] { "About…", "Sobre…", "Über…", "À propos…", "关于…" },
        ["menu.language"] = new[] { "&Language", "&Idioma", "&Sprache", "&Langue", "语言(&L)" },

        // ---- Header banner ----------------------------------------------------------
        ["banner.subtitle"] = new[]
        {
            "Hub Studio — Batch Texture Converter",
            "Hub Studio — Conversor de Texturas em Lote",
            "Hub Studio — Batch-Textur-Konverter",
            "Hub Studio — Convertisseur de textures par lots",
            "Hub Studio — 批量纹理转换器",
        },

        // ---- Destination folders ------------------------------------------------------
        ["dest.groupTitle"] = new[] { "Destination Folders", "Pastas de Destino", "Zielordner", "Dossiers de destination", "目标文件夹" },
        ["dest.browse"] = new[] { "Browse…", "Procurar…", "Durchsuchen…", "Parcourir…", "浏览…" },
        ["dest.folderDialogDesc"] = new[]
        {
            "Select the '{0}' base directory:",
            "Selecione a pasta base de '{0}':",
            "Wählen Sie das Basisverzeichnis für '{0}':",
            "Sélectionnez le dossier de base pour « {0} » :",
            "选择 '{0}' 的基础目录：",
        },
        ["dest.locateGui"] = new[]
        {
            "📁  Locate BG3 GUI Folder…",
            "📁  Localizar Pasta GUI do BG3…",
            "📁  BG3-GUI-Ordner suchen…",
            "📁  Localiser le dossier GUI de BG3…",
            "📁  定位 BG3 GUI 文件夹…",
        },
        ["dest.locateGuiDialogDesc"] = new[]
        {
            "Select your mod's GUI folder (Data\\Mods\\<ModName>\\GUI) - Assets and AssetsLowRes will be set automatically inside it:",
            "Selecione a pasta GUI do seu mod (Data\\Mods\\<NomeDoMod>\\GUI) - Assets e AssetsLowRes serão definidas automaticamente dentro dela:",
            "Wählen Sie den GUI-Ordner Ihres Mods (Data\\Mods\\<ModName>\\GUI) - Assets und AssetsLowRes werden automatisch darin festgelegt:",
            "Sélectionnez le dossier GUI de votre mod (Data\\Mods\\<NomDuMod>\\GUI) - Assets et AssetsLowRes seront définis automatiquement à l'intérieur :",
            "选择你的 mod 的 GUI 文件夹（Data\\Mods\\<ModName>\\GUI）- Assets 和 AssetsLowRes 将自动设置在其中：",
        },

        // ---- Toolbar --------------------------------------------------------------
        ["toolbar.outputExtension"] = new[] { "Output extension:", "Extensão de saída:", "Ausgabe-Erweiterung:", "Extension de sortie :", "输出扩展名：" },
        ["toolbar.convertAll"] = new[] { "▶  Convert All", "▶  Converter Tudo", "▶  Alle konvertieren", "▶  Tout convertir", "▶  全部转换" },
        ["toolbar.cancel"] = new[] { "■  Cancel", "■  Cancelar", "■  Abbrechen", "■  Annuler", "■  取消" },
        ["toolbar.clearList"] = new[] { "Clear List", "Limpar Lista", "Liste leeren", "Vider la liste", "清空列表" },

        // ---- Texconv warning ------------------------------------------------------------
        ["warning.texconvMissing"] = new[]
        {
            "⚠  texconv.exe was not found in the lib\\ folder — conversions will fail until it's added.",
            "⚠  texconv.exe não foi encontrado na pasta lib\\ — as conversões vão falhar até que seja adicionado.",
            "⚠  texconv.exe wurde nicht im lib\\-Ordner gefunden — Konvertierungen schlagen fehl, bis sie hinzugefügt wird.",
            "⚠  texconv.exe est introuvable dans le dossier lib\\ — les conversions échoueront tant qu'il ne sera pas ajouté.",
            "⚠  在 lib\\ 文件夹中未找到 texconv.exe — 添加之前转换将会失败。",
        },

        // ---- Drop zone -----------------------------------------------------------------
        ["dropzone.idle"] = new[]
        {
            "Drag && drop image files here, or click to browse",
            "Arraste e solte arquivos de imagem aqui, ou clique para procurar",
            "Bilddateien hierher ziehen, oder klicken zum Durchsuchen",
            "Glissez-déposez des images ici, ou cliquez pour parcourir",
            "将图像文件拖放到此处，或点击浏览",
        },
        ["dropzone.hover"] = new[] { "Drop to add files", "Solte para adicionar arquivos", "Loslassen, um Dateien hinzuzufügen", "Déposez pour ajouter les fichiers", "松开以添加文件" },

        // ---- List view columns ---------------------------------------------------------
        ["col.subfolder"] = new[] { "Subfolder", "Subpasta", "Unterordner", "Sous-dossier", "子文件夹" },
        ["col.finalName"] = new[] { "Final Name", "Nome Final", "Endgültiger Name", "Nom final", "最终名称" },
        ["col.assetType"] = new[] { "Asset Type", "Tipo de Asset", "Asset-Typ", "Type d'asset", "资源类型" },
        ["col.sourceFile"] = new[] { "Source File", "Arquivo de Origem", "Quelldatei", "Fichier source", "源文件" },
        ["col.ext"] = new[] { "Ext", "Ext", "Erw.", "Ext", "扩展名" },
        ["col.status"] = new[] { "Status", "Status", "Status", "Statut", "状态" },

        // ---- Context menu ---------------------------------------------------------------
        ["ctx.removeSelected"] = new[]
        {
            "🗑️ Remove Selected File(s)",
            "🗑️ Remover Arquivo(s) Selecionado(s)",
            "🗑️ Ausgewählte Datei(en) entfernen",
            "🗑️ Supprimer le(s) fichier(s) sélectionné(s)",
            "🗑️ 删除所选文件",
        },

        // ---- Log panel ------------------------------------------------------------------
        ["log.title"] = new[] { "Activity Log", "Registro de Atividade", "Aktivitätsprotokoll", "Journal d'activité", "活动日志" },
        ["log.copy"] = new[] { "Copy", "Copiar", "Kopieren", "Copier", "复制" },
        ["log.clear"] = new[] { "Clear", "Limpar", "Leeren", "Effacer", "清除" },
        ["log.hide"] = new[] { "▾ Hide", "▾ Ocultar", "▾ Ausblenden", "▾ Masquer", "▾ 隐藏" },
        ["log.show"] = new[] { "▸ Show", "▸ Mostrar", "▸ Einblenden", "▸ Afficher", "▸ 显示" },

        // ---- File dialogs -----------------------------------------------------------------
        ["filedlg.title"] = new[] { "Select images to add", "Selecione as imagens para adicionar", "Bilder zum Hinzufügen auswählen", "Sélectionnez les images à ajouter", "选择要添加的图像" },
        ["filedlg.filterImages"] = new[] { "Image Files", "Arquivos de Imagem", "Bilddateien", "Fichiers image", "图像文件" },
        ["filedlg.filterAll"] = new[] { "All Files", "Todos os Arquivos", "Alle Dateien", "Tous les fichiers", "所有文件" },

        // ---- Message boxes --------------------------------------------------------------
        ["msg.nothingToDo.body"] = new[]
        {
            "Please choose at least one destination folder (Assets or AssetsLowRes).",
            "Escolha pelo menos uma pasta de destino (Assets ou AssetsLowRes).",
            "Bitte wählen Sie mindestens einen Zielordner (Assets oder AssetsLowRes).",
            "Veuillez choisir au moins un dossier de destination (Assets ou AssetsLowRes).",
            "请至少选择一个目标文件夹（Assets 或 AssetsLowRes）。",
        },
        ["msg.nothingToDo.title"] = new[] { "Nothing to do", "Nada a fazer", "Nichts zu tun", "Rien à faire", "无需操作" },
        ["msg.openFolder.missing"] = new[]
        {
            "That folder doesn't exist yet.",
            "Essa pasta ainda não existe.",
            "Dieser Ordner existiert noch nicht.",
            "Ce dossier n'existe pas encore.",
            "该文件夹尚不存在。",
        },
        ["msg.openFolder.title"] = new[] { "Open Folder", "Abrir Pasta", "Ordner öffnen", "Ouvrir le dossier", "打开文件夹" },
        ["msg.conversionRunning.body"] = new[]
        {
            "A conversion is still running. Cancel it and exit?",
            "Uma conversão ainda está em andamento. Cancelar e sair?",
            "Eine Konvertierung läuft noch. Abbrechen und beenden?",
            "Une conversion est toujours en cours. L'annuler et quitter ?",
            "转换仍在进行中。要取消并退出吗？",
        },
        ["msg.conversionRunning.title"] = new[] { "Conversion in progress", "Conversão em andamento", "Konvertierung läuft", "Conversion en cours", "正在转换" },

        // ---- Overwrite dialog -----------------------------------------------------------
        ["overwrite.title"] = new[] { "File already exists", "O arquivo já existe", "Datei existiert bereits", "Le fichier existe déjà", "文件已存在" },
        ["overwrite.fileExists"] = new[]
        {
            "\"{0}\" already exists in:",
            "\"{0}\" já existe em:",
            "\"{0}\" existiert bereits in:",
            "« {0} » existe déjà dans :",
            "\"{0}\" 已存在于：",
        },
        ["overwrite.question"] = new[]
        {
            "Do you want to overwrite it?",
            "Deseja substituí-lo?",
            "Möchten Sie sie überschreiben?",
            "Voulez-vous l'écraser ?",
            "是否要覆盖它？",
        },
        ["overwrite.yes"] = new[] { "Yes", "Sim", "Ja", "Oui", "是" },
        ["overwrite.yesToAll"] = new[] { "Yes to All", "Sim para Todos", "Ja für alle", "Oui pour tout", "全部是" },
        ["overwrite.no"] = new[] { "No", "Não", "Nein", "Non", "否" },
        ["overwrite.noToAll"] = new[] { "No to All", "Não para Todos", "Nein für alle", "Non pour tout", "全部否" },

        // ---- About dialog -----------------------------------------------------------------
        ["about.title"] = new[] { "About", "Sobre", "Über", "À propos", "关于" },
        ["about.version"] = new[] { "Version {0}", "Versão {0}", "Version {0}", "Version {0}", "版本 {0}" },
        ["about.texconvFound"] = new[] { "texconv.exe: found", "texconv.exe: encontrado", "texconv.exe: gefunden", "texconv.exe : trouvé", "texconv.exe：已找到" },
        ["about.texconvMissing"] = new[]
        {
            "texconv.exe: NOT found in lib\\",
            "texconv.exe: NÃO encontrado em lib\\",
            "texconv.exe: NICHT gefunden in lib\\",
            "texconv.exe : INTROUVABLE dans lib\\",
            "texconv.exe：在 lib\\ 中未找到",
        },
        ["about.close"] = new[] { "Close", "Fechar", "Schließen", "Fermer", "关闭" },

        // ---- Donate dialog ----------------------------------------------------------------
        ["donate.title"] = new[] { "Support This Project", "Apoie Este Projeto", "Dieses Projekt unterstützen", "Soutenir ce projet", "支持本项目" },
        ["donate.heading"] = new[]
        {
            "💙  Enjoying BG3 DDS Convert?",
            "💙  Curtindo o BG3 DDS Convert?",
            "💙  Gefällt dir BG3 DDS Convert?",
            "💙  Vous aimez BG3 DDS Convert ?",
            "💙  喜欢 BG3 DDS Convert 吗？",
        },
        ["donate.message"] = new[]
        {
            "This tool is free and made with care for the BG3 modding community.\nIf it's saved you time, a small donation is always appreciated - thank you!",
            "Esta ferramenta é gratuita e feita com carinho para a comunidade de mods de BG3.\nSe ela economizou seu tempo, qualquer doação é muito bem-vinda - obrigado!",
            "Dieses Tool ist kostenlos und mit Sorgfalt für die BG3-Modding-Community entwickelt.\nWenn es dir Zeit gespart hat, ist eine kleine Spende immer willkommen - danke!",
            "Cet outil est gratuit et conçu avec soin pour la communauté de modding BG3.\nS'il vous a fait gagner du temps, un petit don est toujours apprécié - merci !",
            "此工具免费提供，是为 BG3 模组社区用心制作的。\n如果它为你节省了时间，欢迎小额捐赠支持 - 谢谢！",
        },
        ["donate.button"] = new[]
        {
            "💙  Donate with PayPal",
            "💙  Doar com PayPal",
            "💙  Mit PayPal spenden",
            "💙  Faire un don via PayPal",
            "💙  通过 PayPal 捐赠",
        },
        ["donate.later"] = new[] { "Maybe Later", "Talvez Depois", "Vielleicht später", "Peut-être plus tard", "以后再说" },
        ["donate.error"] = new[]
        {
            "Could not open the browser: {0}",
            "Não foi possível abrir o navegador: {0}",
            "Der Browser konnte nicht geöffnet werden: {0}",
            "Impossible d'ouvrir le navigateur : {0}",
            "无法打开浏览器：{0}",
        },

        // ---- Help dialog ------------------------------------------------------------------
        ["help.title"] = new[] { "How to Use", "Como Usar", "Bedienungsanleitung", "Comment utiliser", "使用说明" },
        ["help.appTitle"] = new[] { "BG3 DDS Convert", "BG3 DDS Convert", "BG3 DDS Convert", "BG3 DDS Convert", "BG3 DDS Convert" },
        ["help.overview"] = new[]
        {
            "A batch conversion tool for Baldur's Gate 3 modders to convert images into .dds files with dual-resolution output, using the exact DDS settings BG3 itself uses for each icon type.",
            "Uma ferramenta de conversão em lote para modders de Baldur's Gate 3 converterem imagens em arquivos .dds com saída em dupla resolução, usando exatamente as mesmas configurações de DDS que o próprio BG3 usa para cada tipo de ícone.",
            "Ein Batch-Konvertierungstool für Baldur's-Gate-3-Modder, um Bilder in .dds-Dateien mit Doppelauflösungs-Export umzuwandeln - mit genau den DDS-Einstellungen, die BG3 selbst für jeden Icon-Typ verwendet.",
            "Un outil de conversion par lots pour les moddeurs de Baldur's Gate 3, permettant de convertir des images en fichiers .dds avec export en double résolution, en utilisant exactement les réglages DDS que BG3 utilise lui-même pour chaque type d'icône.",
            "面向《博德之门3》模组制作者的批量转换工具，可将图像转换为双分辨率输出的 .dds 文件，并使用 BG3 本身针对每种图标类型所采用的精确 DDS 设置。",
        },
        ["help.step1.header"] = new[] { "1. Set Destination Folders", "1. Defina as Pastas de Destino", "1. Zielordner festlegen", "1. Définir les dossiers de destination", "1. 设置目标文件夹" },
        ["help.step1.body"] = new[]
        {
            "Assets: your primary mod asset folder. AssetsLowRes: your downscaled mod asset folder. Paths are saved automatically for future sessions.",
            "Assets: sua pasta principal de assets do mod. AssetsLowRes: sua pasta de assets em resolução reduzida. Os caminhos são salvos automaticamente para as próximas sessões.",
            "Assets: Ihr primärer Mod-Asset-Ordner. AssetsLowRes: Ihr herunterskalierter Mod-Asset-Ordner. Die Pfade werden automatisch für zukünftige Sitzungen gespeichert.",
            "Assets : votre dossier principal d'assets du mod. AssetsLowRes : votre dossier d'assets en résolution réduite. Les chemins sont enregistrés automatiquement pour les prochaines sessions.",
            "Assets：你的主要 mod 资源文件夹。AssetsLowRes：你的低分辨率 mod 资源文件夹。路径会自动保存以供下次使用。",
        },
        ["help.step2.header"] = new[] { "2. Add Images", "2. Adicione as Imagens", "2. Bilder hinzufügen", "2. Ajouter des images", "2. 添加图像" },
        ["help.step2.body"] = new[]
        {
            "Drag and drop image files (.png, .jpg, .bmp, .tga, .tiff, .hdr, .dds) into the window, or click the drop area to browse. Each file lands with an empty subfolder (destination root) and its filename as the output name, unless the naming pattern below is used.",
            "Arraste e solte arquivos de imagem (.png, .jpg, .bmp, .tga, .tiff, .hdr, .dds) na janela, ou clique na área de soltar para procurar. Cada arquivo entra com a subpasta vazia (raiz do destino) e o nome do arquivo como nome de saída, a menos que o padrão de nomenclatura abaixo seja usado.",
            "Ziehen Sie Bilddateien (.png, .jpg, .bmp, .tga, .tiff, .hdr, .dds) in das Fenster, oder klicken Sie auf den Ablagebereich zum Durchsuchen. Jede Datei landet mit leerem Unterordner (Zielstamm) und ihrem Dateinamen als Ausgabename, sofern nicht das unten stehende Namensmuster verwendet wird.",
            "Glissez-déposez des fichiers image (.png, .jpg, .bmp, .tga, .tiff, .hdr, .dds) dans la fenêtre, ou cliquez sur la zone de dépôt pour parcourir. Chaque fichier arrive avec un sous-dossier vide (racine de destination) et son nom de fichier comme nom de sortie, sauf si le modèle de nommage ci-dessous est utilisé.",
            "将图像文件（.png、.jpg、.bmp、.tga、.tiff、.hdr、.dds）拖放到窗口中，或点击拖放区域进行浏览。除非使用下方的命名模式，否则每个文件添加时子文件夹为空（目标根目录），输出名称为其文件名。",
        },
        ["help.step3.header"] = new[] { "3. Set Subfolder & Output Name", "3. Defina a Subpasta e o Nome de Saída", "3. Unterordner & Ausgabename festlegen", "3. Définir le sous-dossier et le nom de sortie", "3. 设置子文件夹和输出名称" },
        ["help.step3.body"] = new[]
        {
            "Double-click a row's Subfolder cell to type the destination subfolder (e.g. ClassIcons\\hotbar), or the Final Name cell (or press F2) to rename the output file.\n\nTip: name the source file itself \"%Subfolder1%Subfolder2#FinalName.png\" before adding it - the app splits on '%' for nested subfolders and takes everything after '#' as the final name, filling both cells automatically.",
            "Dê duplo clique na célula de Subpasta de uma linha para digitar a subpasta de destino (ex.: ClassIcons\\hotbar), ou na célula de Nome Final (ou pressione F2) para renomear o arquivo de saída.\n\nDica: nomeie o próprio arquivo de origem como \"%Subpasta1%Subpasta2#NomeFinal.png\" antes de adicioná-lo - o app separa por '%' para subpastas aninhadas e usa tudo depois do '#' como nome final, preenchendo as duas células automaticamente.",
            "Doppelklicken Sie auf die Unterordner-Zelle einer Zeile, um den Zielunterordner einzugeben (z. B. ClassIcons\\hotbar), oder auf die Zelle „Endgültiger Name“ (oder drücken Sie F2), um die Ausgabedatei umzubenennen.\n\nTipp: Benennen Sie die Quelldatei selbst \"%Unterordner1%Unterordner2#Endname.png\", bevor Sie sie hinzufügen - die App trennt bei '%' für verschachtelte Unterordner und verwendet alles nach '#' als Endname, wodurch beide Zellen automatisch ausgefüllt werden.",
            "Double-cliquez sur la cellule Sous-dossier d'une ligne pour saisir le sous-dossier de destination (ex. ClassIcons\\hotbar), ou sur la cellule Nom final (ou appuyez sur F2) pour renommer le fichier de sortie.\n\nAstuce : nommez le fichier source lui-même \"%Sousdossier1%Sousdossier2#NomFinal.png\" avant de l'ajouter - l'application découpe sur '%' pour les sous-dossiers imbriqués et prend tout ce qui suit '#' comme nom final, remplissant les deux cellules automatiquement.",
            "双击某行的“子文件夹”单元格以输入目标子文件夹（例如 ClassIcons\\hotbar），或双击“最终名称”单元格（或按 F2）以重命名输出文件。\n\n提示：在添加源文件之前，将其命名为 \"%子文件夹1%子文件夹2#最终名称.png\" - 应用会按 '%' 拆分出嵌套子文件夹，并将 '#' 之后的所有内容作为最终名称，自动填充这两个单元格。",
        },
        ["help.step4.header"] = new[] { "4. Choose the Asset Type", "4. Escolha o Tipo de Asset", "4. Asset-Typ wählen", "4. Choisir le type d'asset", "4. 选择资源类型" },
        ["help.step4.body"] = new[]
        {
            "Double-click a row's Asset Type cell and pick the specific category that matches the icon you're replacing (Class Icon, CC Background Icon, Tooltip Icon...) - picking one also fills in the correct Subfolder automatically. Dropping a file whose pixel size unambiguously matches one category already does this for you. When nothing fits, \"Custom / Other (BC7)\" (the default) leaves the Subfolder for you to set by hand.",
            "Dê duplo clique na célula de Tipo de Asset de uma linha e escolha a categoria específica que corresponde ao ícone que você está substituindo (Class Icon, CC Background Icon, Tooltip Icon...) - escolher uma também preenche a Subpasta correta automaticamente. Adicionar um arquivo cujo tamanho em pixels corresponda sem ambiguidade a uma categoria já faz isso por você. Quando nada se encaixa, \"Custom / Other (BC7)\" (o padrão) deixa a Subpasta para você definir manualmente.",
            "Doppelklicken Sie auf die Asset-Typ-Zelle einer Zeile und wählen Sie die spezifische Kategorie, die zum ersetzten Icon passt (Class Icon, CC Background Icon, Tooltip Icon...) - die Auswahl füllt automatisch auch den richtigen Unterordner aus. Das Hinzufügen einer Datei, deren Pixelgröße eindeutig zu einer Kategorie passt, erledigt dies bereits automatisch. Wenn nichts passt, belässt „Custom / Other (BC7)“ (die Standardeinstellung) den Unterordner zur manuellen Eingabe.",
            "Double-cliquez sur la cellule Type d'asset d'une ligne et choisissez la catégorie précise correspondant à l'icône que vous remplacez (Class Icon, CC Background Icon, Tooltip Icon...) - ce choix remplit aussi automatiquement le bon sous-dossier. L'ajout d'un fichier dont la taille en pixels correspond sans ambiguïté à une catégorie le fait déjà automatiquement. Si rien ne correspond, « Custom / Other (BC7) » (l'option par défaut) laisse le sous-dossier à définir manuellement.",
            "双击某行的“资源类型”单元格，选择与你要替换的图标相匹配的具体类别（Class Icon、CC Background Icon、Tooltip Icon 等）- 选择后会自动填入正确的子文件夹。添加的文件若像素尺寸明确匹配某个类别，也会自动完成此操作。如果都不合适，默认的 \"Custom / Other (BC7)\" 会让你手动设置子文件夹。",
        },
        ["help.step5.header"] = new[] { "5. Run Conversion", "5. Execute a Conversão", "5. Konvertierung ausführen", "5. Lancer la conversion", "5. 运行转换" },
        ["help.step5.body"] = new[]
        {
            "Select your preferred file extension format (.DDS or .dds) from the toolbar, then click \"Convert All\". Click the same button (now \"Cancel\") to stop a running batch.",
            "Selecione o formato de extensão de arquivo preferido (.DDS ou .dds) na barra de ferramentas e clique em \"Converter Tudo\". Clique no mesmo botão (agora \"Cancelar\") para interromper um lote em andamento.",
            "Wählen Sie in der Symbolleiste das bevorzugte Dateierweiterungsformat (.DDS oder .dds) und klicken Sie dann auf „Alle konvertieren“. Klicken Sie erneut auf denselben Button (jetzt „Abbrechen“), um einen laufenden Stapel zu stoppen.",
            "Sélectionnez le format d'extension de fichier souhaité (.DDS ou .dds) dans la barre d'outils, puis cliquez sur « Tout convertir ». Cliquez sur le même bouton (désormais « Annuler ») pour arrêter un lot en cours.",
            "在工具栏中选择所需的文件扩展名格式（.DDS 或 .dds），然后点击“全部转换”。再次点击同一按钮（此时显示为“取消”）可停止正在进行的批处理。",
        },
        ["help.assetTypeIntro.header"] = new[] { "Choosing an Asset Type", "Escolhendo um Tipo de Asset", "Einen Asset-Typ wählen", "Choisir un type d'asset", "选择资源类型" },
        ["help.assetTypeIntro.body"] = new[]
        {
            "BG3 doesn't use one single DDS format for every icon - different parts of the UI expect different settings, and using the wrong one is what makes a converted icon look too dark, too light, or simply not show up. The two options below were determined by extracting real icon files from the game itself (Icons.pak / Game.pak, patch 8) and reading their DDS headers directly, not by guessing.",
            "O BG3 não usa um único formato DDS para todos os ícones - partes diferentes da interface esperam configurações diferentes, e usar a errada é o que faz um ícone convertido ficar escuro demais, claro demais, ou simplesmente não aparecer. As duas opções abaixo foram determinadas extraindo arquivos de ícones reais do próprio jogo (Icons.pak / Game.pak, patch 8) e lendo os cabeçalhos DDS diretamente, sem achismo.",
            "BG3 verwendet nicht ein einziges DDS-Format für alle Icons - verschiedene UI-Bereiche erwarten unterschiedliche Einstellungen, und die falsche Wahl ist der Grund, warum ein konvertiertes Icon zu dunkel, zu hell oder gar nicht angezeigt wird. Die beiden folgenden Optionen wurden ermittelt, indem echte Icon-Dateien aus dem Spiel selbst extrahiert (Icons.pak / Game.pak, Patch 8) und deren DDS-Header direkt ausgelesen wurden - nicht durch Raten.",
            "BG3 n'utilise pas un seul format DDS pour toutes les icônes - différentes parties de l'interface attendent des réglages différents, et utiliser le mauvais est ce qui rend une icône convertie trop sombre, trop claire, ou tout simplement invisible. Les deux options ci-dessous ont été déterminées en extrayant de vrais fichiers d'icônes du jeu lui-même (Icons.pak / Game.pak, patch 8) et en lisant directement leurs en-têtes DDS, sans deviner.",
            "BG3 并非所有图标都使用同一种 DDS 格式 - 界面的不同部分需要不同的设置，用错格式正是导致转换后的图标偏暗、偏亮或干脆不显示的原因。以下两个选项是通过从游戏本体（Icons.pak / Game.pak，patch 8）提取真实图标文件并直接读取其 DDS 头信息确定的，而非猜测得出。",
        },
        ["help.uiIcon.header"] = new[]
        {
            "UI Icon (BC7) — the default, use this for almost everything",
            "UI Icon (BC7) — padrão, use para quase tudo",
            "UI Icon (BC7) — Standard, für fast alles verwenden",
            "UI Icon (BC7) — par défaut, à utiliser pour presque tout",
            "UI Icon (BC7) — 默认选项，适用于几乎所有情况",
        },
        ["help.uiIcon.body"] = new[]
        {
            "This is the format BG3 uses for essentially every icon you click on, hover over, or see on the hotbar. Pick this for:\n• Class and Subclass icons (Barbarian, Bard, Abjuration School, Archfey, Battle Master, Beast Master...)\n• Hotbar-sized class/subclass icons\n• Action Resource icons (Action Points, Ki Points, Bardic Inspiration, Channel Divinity, Sorcery Points, Lay on Hands Charges...)\n• Ability and Skill icons (Strength, Charisma, Proficiency, Expertise, Acrobatics, Arcana...)\n• Individual item icons (weapons, armor, potions, scrolls)\n\nIf you're not sure what your icon is, this is almost always the right choice.",
            "Este é o formato que o BG3 usa para praticamente todo ícone em que você clica, passa o mouse, ou vê na hotbar. Escolha esta opção para:\n• Ícones de Classe e Subclasse (Barbarian, Bard, Abjuration School, Archfey, Battle Master, Beast Master...)\n• Ícones de classe/subclasse em tamanho de hotbar\n• Ícones de Action Resource (Action Points, Ki Points, Bardic Inspiration, Channel Divinity, Sorcery Points, Lay on Hands Charges...)\n• Ícones de Habilidade e Perícia (Strength, Charisma, Proficiency, Expertise, Acrobatics, Arcana...)\n• Ícones de item individuais (armas, armaduras, poções, pergaminhos)\n\nSe não tiver certeza do que é o seu ícone, esta é quase sempre a escolha certa.\n\n(Os nomes acima ficam em inglês de propósito - são os mesmos nomes de pasta usados dentro dos arquivos do jogo, então bater com eles ajuda a confirmar que você escolheu a pasta certa.)",
            "Dies ist das Format, das BG3 für praktisch jedes Icon verwendet, auf das Sie klicken, das Sie mit der Maus berühren, oder das Sie auf der Hotbar sehen. Wählen Sie dies für:\n• Klassen- und Unterklassen-Icons (Barbarian, Bard, Abjuration School, Archfey, Battle Master, Beast Master...)\n• Klassen-/Unterklassen-Icons in Hotbar-Größe\n• Action-Resource-Icons (Action Points, Ki Points, Bardic Inspiration, Channel Divinity, Sorcery Points, Lay on Hands Charges...)\n• Fähigkeits- und Fertigkeits-Icons (Strength, Charisma, Proficiency, Expertise, Acrobatics, Arcana...)\n• Einzelne Item-Icons (Waffen, Rüstungen, Tränke, Schriftrollen)\n\nWenn Sie nicht sicher sind, um welches Icon es sich handelt, ist dies fast immer die richtige Wahl.\n\n(Die obigen Namen bleiben absichtlich auf Englisch - es sind dieselben Ordnernamen, die in den Spieldateien verwendet werden, sodass der Abgleich hilft zu bestätigen, dass Sie den richtigen Ordner gewählt haben.)",
            "C'est le format que BG3 utilise pour pratiquement chaque icône sur laquelle vous cliquez, que vous survolez, ou que vous voyez sur la barre de raccourcis. Choisissez ceci pour :\n• Les icônes de Classe et Sous-classe (Barbarian, Bard, Abjuration School, Archfey, Battle Master, Beast Master...)\n• Les icônes de classe/sous-classe format barre de raccourcis\n• Les icônes de Ressource d'Action (Action Points, Ki Points, Bardic Inspiration, Channel Divinity, Sorcery Points, Lay on Hands Charges...)\n• Les icônes de Caractéristique et Compétence (Strength, Charisma, Proficiency, Expertise, Acrobatics, Arcana...)\n• Les icônes d'objets individuels (armes, armures, potions, parchemins)\n\nSi vous n'êtes pas sûr du type de votre icône, c'est presque toujours le bon choix.\n\n(Les noms ci-dessus restent volontairement en anglais - ce sont les mêmes noms de dossiers utilisés dans les fichiers du jeu, donc les faire correspondre aide à confirmer que vous avez choisi le bon dossier.)",
            "这是 BG3 用于几乎所有你点击、悬停查看或在快捷栏上看到的图标的格式。以下情况请选择此项：\n• 职业和子职业图标（Barbarian、Bard、Abjuration School、Archfey、Battle Master、Beast Master 等）\n• 快捷栏尺寸的职业/子职业图标\n• Action Resource 图标（Action Points、Ki Points、Bardic Inspiration、Channel Divinity、Sorcery Points、Lay on Hands Charges 等）\n• 属性与技能图标（Strength、Charisma、Proficiency、Expertise、Acrobatics、Arcana 等）\n• 单个物品图标（武器、护甲、药水、卷轴）\n\n如果不确定你的图标属于哪种类型，选这个几乎总是对的。\n\n（以上名称有意保留英文 - 它们与游戏文件内部使用的文件夹名称相同，对照这些名称有助于确认你选对了文件夹。）",
        },
        ["help.ccIcon.header"] = new[]
        {
            "CC Resource Icon (Legacy DXT1) — only for Character Creation's alternate resource icons",
            "CC Resource Icon (Legacy DXT1) — só para os ícones alternativos de recurso da Criação de Personagem",
            "CC Resource Icon (Legacy DXT1) — nur für die alternativen Ressourcen-Icons der Charaktererstellung",
            "CC Resource Icon (Legacy DXT1) — uniquement pour les icônes de ressource alternatives de la création de personnage",
            "CC Resource Icon (Legacy DXT1) — 仅用于角色创建界面的替代资源图标",
        },
        ["help.ccIcon.body"] = new[]
        {
            "BG3 keeps a second, simpler copy of the resource icons specifically for the Character Creation screen (GUI\\Assets\\CC\\icons_resources, 128x128) - same resources as above, but an older, lower-quality format (DXT1, no smooth transparency). \"Controller Icon Background\" uses this same legacy format too, for a different folder. Only pick these if you're specifically replacing a file inside that exact folder.",
            "O BG3 mantém uma segunda cópia, mais simples, dos ícones de recurso especificamente para a tela de Criação de Personagem (GUI\\Assets\\CC\\icons_resources, 128x128) - os mesmos recursos de acima, mas em um formato mais antigo e de qualidade inferior (DXT1, sem transparência suave). \"Controller Icon Background\" usa esse mesmo formato legado, para outra pasta. Só escolha essas opções se estiver substituindo especificamente um arquivo dentro dessa pasta exata.",
            "BG3 verwaltet speziell für den Charaktererstellungs-Bildschirm eine zweite, einfachere Kopie der Ressourcen-Icons (GUI\\Assets\\CC\\icons_resources, 128x128) - dieselben Ressourcen wie oben, aber in einem älteren Format mit geringerer Qualität (DXT1, keine weiche Transparenz). „Controller Icon Background“ verwendet dasselbe Legacy-Format für einen anderen Ordner. Wählen Sie diese Optionen nur, wenn Sie gezielt eine Datei innerhalb dieses genauen Ordners ersetzen.",
            "BG3 conserve une seconde copie, plus simple, des icônes de ressource spécifiquement pour l'écran de création de personnage (GUI\\Assets\\CC\\icons_resources, 128x128) - les mêmes ressources que ci-dessus, mais dans un format plus ancien et de qualité inférieure (DXT1, sans transparence lisse). « Controller Icon Background » utilise ce même format hérité, pour un autre dossier. Ne choisissez ces options que si vous remplacez spécifiquement un fichier situé dans ce dossier exact.",
            "BG3 专门为角色创建界面保留了第二套更简单的资源图标副本（GUI\\Assets\\CC\\icons_resources，128x128）- 与上面相同的资源，但使用较旧、质量较低的格式（DXT1，无平滑透明）。\"Controller Icon Background\" 用于另一个文件夹，也使用同样的旧格式。仅当你要替换的文件确实位于该确切文件夹内时才选择这些选项。",
        },
        ["help.atlas.header"] = new[]
        {
            "Not supported: packed icon sheets / atlases",
            "Não suportado: folhas/atlas de ícones empacotados",
            "Nicht unterstützt: gepackte Icon-Sheets / Atlanten",
            "Non pris en charge : planches / atlas d'icônes empaquetées",
            "不支持：打包的图标表/图集",
        },
        ["help.atlas.body"] = new[]
        {
            "Some game icons (all item and spell icons together, portraits) ship as one large sheet - e.g. Icons_Items.dds, Icons_Skills.dds - with each icon referenced by a coordinate rectangle inside it. This app converts one source image into one destination file; it does not pack multiple icons into a shared sheet. Use the official BG3 Toolkit for that workflow - it already handles atlas packing well.",
            "Alguns ícones do jogo (todos os ícones de item e magia juntos, retratos) vêm como uma única folha grande - ex.: Icons_Items.dds, Icons_Skills.dds - com cada ícone referenciado por um retângulo de coordenadas dentro dela. Este app converte uma imagem de origem em um único arquivo de destino; ele não empacota vários ícones em uma folha compartilhada. Use o Toolkit oficial do BG3 para esse fluxo de trabalho - ele já lida bem com o empacotamento de atlas.",
            "Manche Spiel-Icons (alle Item- und Zauber-Icons zusammen, Porträts) werden als ein großes Sheet ausgeliefert - z. B. Icons_Items.dds, Icons_Skills.dds - wobei jedes Icon durch ein Koordinatenrechteck darin referenziert wird. Diese App konvertiert ein Quellbild in eine Zieldatei; sie packt keine mehreren Icons in ein gemeinsames Sheet. Verwenden Sie dafür das offizielle BG3-Toolkit - es beherrscht das Atlas-Packing bereits gut.",
            "Certaines icônes du jeu (toutes les icônes d'objets et de sorts réunies, les portraits) sont livrées sous forme d'une grande planche - ex. Icons_Items.dds, Icons_Skills.dds - chaque icône étant référencée par un rectangle de coordonnées à l'intérieur. Cette application convertit une image source en un seul fichier de destination ; elle n'assemble pas plusieurs icônes dans une planche partagée. Utilisez le Toolkit officiel de BG3 pour ce flux de travail - il gère déjà très bien l'assemblage d'atlas.",
            "部分游戏图标（所有物品和法术图标合在一起、肖像画）以一张大图集形式提供 - 例如 Icons_Items.dds、Icons_Skills.dds - 每个图标通过其中的坐标矩形来引用。本应用将一个源图像转换为一个目标文件；它不会将多个图标打包进共享图集中。请使用官方 BG3 Toolkit 完成该工作流程 - 它已经能很好地处理图集打包。",
        },
        ["help.tips.header"] = new[] { "Tips", "Dicas", "Tipps", "Astuces", "小提示" },
        ["help.tips.body"] = new[]
        {
            "• Click a column header to sort by that column; click again to reverse the order.\n• Right-click selected rows to remove them from the list.\n• The Activity Log at the bottom records every conversion - copy it if you need to troubleshoot.\n• texconv.exe must remain in the \"lib\" folder next to the application for conversions to work.",
            "• Clique no cabeçalho de uma coluna para ordenar por ela; clique de novo para inverter a ordem.\n• Clique com o botão direito nas linhas selecionadas para removê-las da lista.\n• O Registro de Atividade na parte inferior registra cada conversão - copie-o se precisar investigar um problema.\n• O texconv.exe precisa permanecer na pasta \"lib\" ao lado do aplicativo para que as conversões funcionem.",
            "• Klicken Sie auf eine Spaltenüberschrift, um danach zu sortieren; erneut klicken kehrt die Reihenfolge um.\n• Rechtsklicken Sie auf ausgewählte Zeilen, um sie aus der Liste zu entfernen.\n• Das Aktivitätsprotokoll unten zeichnet jede Konvertierung auf - kopieren Sie es bei Problemen zur Fehlersuche.\n• texconv.exe muss im Ordner \"lib\" neben der Anwendung bleiben, damit Konvertierungen funktionieren.",
            "• Cliquez sur l'en-tête d'une colonne pour trier selon celle-ci ; cliquez à nouveau pour inverser l'ordre.\n• Effectuez un clic droit sur les lignes sélectionnées pour les retirer de la liste.\n• Le journal d'activité en bas enregistre chaque conversion - copiez-le si vous devez résoudre un problème.\n• texconv.exe doit rester dans le dossier \"lib\" à côté de l'application pour que les conversions fonctionnent.",
            "• 点击列标题可按该列排序；再次点击可反转排序顺序。\n• 右键点击所选行可将其从列表中移除。\n• 底部的活动日志记录了每一次转换 - 如需排查问题可以复制它。\n• texconv.exe 必须保留在应用程序旁边的 \"lib\" 文件夹中，转换功能才能正常工作。",
        },
    };
}
