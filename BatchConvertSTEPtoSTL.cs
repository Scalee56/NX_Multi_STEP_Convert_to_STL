// =============================================================================
// NX Open Journal - Conversione massiva STEP -> STL (v12)
// Basato sul journal originale "journal.cs" (export singolo STL registrato in NX),
// esteso per scorrere automaticamente tutti i file .stp/.step di una cartella.
//
// COSA E' STATO CORRETTO/AGGIUNTO IN QUESTA VERSIONE (v12):
// - RIMOSSA definitivamente LauncherForm (System.Windows.Forms.Form): confermato
//   su questa installazione NX, anche a sessione appena riavviata e con ogni
//   mitigazione provata, che nessuna Form puo' mai aprirsi qui (mismatch
//   strutturale tra le versioni di System.Windows.Forms e System.Drawing.Common
//   caricate dal processo NX). Nessun trucco lato journal puo' risolverlo.
// - NUOVO: al posto della Form, la GUI vera e propria (configurazione cartelle
//   e opzioni avanzate, risoluzione conflitti, riepilogo finale) viene mostrata
//   lanciando powershell.exe come PROCESSO SEPARATO da quello di NX. Windows
//   PowerShell gira su .NET Framework classico, dove System.Windows.Forms e
//   System.Drawing sono sempre la stessa coppia coerente: lo stesso crash non
//   puo' quindi verificarsi li'. Il journal scrive uno script .ps1 in una
//   cartella temporanea (nessun diritto di amministratore richiesto: sia
//   l'avvio di powershell.exe sia la scrittura in %TEMP% sono operazioni
//   normali di un utente standard), lo lancia in attesa sincrona, e si scambia
//   dati con esso tramite semplici file di testo "chiave=valore" (cartelle
//   scelte, opzioni avanzate, decisione sui conflitti). Se per qualunque
//   motivo PowerShell non fosse disponibile in un dato ambiente, il journal lo
//   rileva e ripiega automaticamente, in modo trasparente, sul flusso a soli
//   MessageBox/FolderBrowserDialog (RunFallbackFlow) gia' presente dalla v9,
//   con la stessa identica logica "chiedi solo se serve".
// - Il pannello di avanzamento in tempo reale con barra di progresso (che
//   viveva solo dentro LauncherForm) non esiste piu': l'avanzamento resta
//   visibile nella Listing Window di NX e nel file di log, aggiornati riga per
//   riga come gia' avveniva. L'annullamento a meta' batch resta possibile
//   creando un file CANCEL.txt nella cartella di output (introdotto in v11).
//
// COSA ERA STATO CORRETTO/AGGIUNTO IN v11:
// - Prima conferma (poi rivelatasi definitiva in v12) che nessuna Form poteva
//   aprirsi su questa installazione NX. Portate nel fallback le funzionalita'
//   che vivevano solo nella Form: annullamento via file marker CANCEL.txt, e
//   una domanda opzionale per l'export dei corpi non chiusi.
//
// COSA ERA STATO CORRETTO/AGGIUNTO IN v10:
// - NUOVO: la finestra grafica (LauncherForm) ora gestisce l'INTERO flusso in
//   un'unica finestra, senza mai chiudersi e riaprirsi: configurazione
//   cartelle -> (se servono) risoluzione conflitti -> avanzamento della
//   conversione in tempo reale -> riepilogo finale, tutto nella stessa
//   finestra. Prima l'avanzamento della conversione si vedeva solo nella
//   Listing Window di NX (una finestra separata, tecnica); ora un pannello
//   dedicato mostra una barra di avanzamento, il file corrente e il log
//   completo in tempo reale (lo stesso testo che va anche nella Listing
//   Window e nel file di log), con un bottone "Annulla" che interrompe la
//   conversione in modo sicuro al confine tra un file STEP e il successivo
//   (mai a meta' esportazione). A fine conversione lo stesso pannello mostra
//   il riepilogo (quanti file riusciti/falliti, quanti STL scritti) con un
//   bottone per aprire direttamente la cartella di output.
// - NUOVO: pannello "Opzioni avanzate" (nascosto di default) nella schermata
//   di configurazione, per modificare le tolleranze STL (chordal, adjacency,
//   angular) e se esportare anche le superfici non chiuse direttamente dalla
//   GUI, senza piu' dover editare questo file per il caso comune di voler
//   cambiare questi valori per una singola esecuzione.
// - Rimangono finestre separate (MessageBox/FolderBrowserDialog) solo per le
//   domande puntuali che lo richiedono davvero (cartella non trovata, errori,
//   selezione di una cartella) - mai per il flusso principale.
//
// COSA ERA GIA' PRESENTE IN v9:
// - NUOVO: il journal ora si apre con una GUI (WinForms) come vero punto di
//   ingresso: alla riproduzione (Tools > Journal > Play) appare subito una
//   finestra con le cartelle di input/output (modificabili con pulsanti
//   "Sfoglia", precompilate con i valori di default configurati sotto) e un
//   pulsante "Avvia scansione". Non serve piu' modificare il file .cs a mano
//   per il caso comune di cartelle diverse da quelle di default.
// - NUOVO: scansione preventiva (PreScanConflicts), SENZA aprire alcuna parte
//   NX: confronta i file STEP nella cartella di input con quanto gia'
//   presente nella cartella di output (sia nella vecchia convenzione
//   "piatta" v8, sia nella nuova convenzione "raggruppata" v9 - vedi sotto),
//   usando l'indice persistente component_index.txt per gli assiemi gia'
//   noti. Il risultato viene mostrato in una finestra con una lista (una
//   riga per file STEP: Nuovo / OK / CONFLITTO) e un riepilogo numerico.
// - NUOVO: dopo la scansione, l'utente fa UNA scelta valida per l'intero
//   batch (non piu' un semplice skip silenzioso come in v8):
//     - "Interrompi": esce senza scrivere alcun file.
//     - "Sovrascrivi": procede esattamente come oggi, ma i file gia'
//       esistenti (i conflitti rilevati dalla scansione) vengono
//       sovrascritti invece di essere saltati.
//     - "Copia in nuova cartella": l'INTERO output di questo run (non solo i
//       file in conflitto) viene scritto in una sottocartella generata
//       automaticamente con timestamp (es. "Export_2026-09-11_143000"),
//       lasciando la cartella di output configurata completamente intatta.
//       Questa nuova cartella riparte con un proprio component_index.txt
//       vuoto (run indipendente, nessuna storia ereditata).
//   Una collisione TRA DUE file prodotti nello stesso run (non contro output
//   di run precedenti) viene invece sempre segnalata e saltata, in qualunque
//   modalita', per non sovrascrivere mai silenziosamente un file appena
//   scritto in questo stesso batch.
// - NUOVO: raggruppamento automatico dell'output. Quando una sorgente (uno
//   STEP con corpi diretti, oppure un singolo componente di un assieme)
//   produce PIU' DI UN file totale (corpi solidi + corpi non chiusi
//   sommati), tutti i suoi file finiscono in una sottocartella dedicata
//   (es. "PartXYZ\"); i corpi non chiusi, quando la sorgente e' raggruppata,
//   finiscono ulteriormente annidati in una sotto-sottocartella
//   "000_Not_Closed_Mesh" dentro quella dedicata. Quando la sorgente produce
//   un solo file totale, il comportamento resta piatto esattamente come
//   nelle versioni precedenti (nessuna sottocartella).
//
// COSA ERA GIA' PRESENTE IN v8:
// - FIX: collisioni di naming tra STEP diversi con un componente omonimo
//   vengono disambiguate con un prefisso, invece di essere saltate in
//   silenzio dalla protezione anti-sovrascrittura.
// - FIX: un componente con sia sotto-componenti sia corpi propri esporta
//   anche i corpi propri, invece di essere trattato come puro contenitore.
// - FIX: risorse NX (STLCreator, PartLoadStatus) rilasciate in blocchi
//   finally anche in caso di errore.
// - FIX: log scritto in modo incrementale (append riga per riga) invece che
//   solo a fine esecuzione.
// - MIGLIORATA: CleanUpFacetedFacesAndEdges() chiamata una sola volta per
//   gruppo di corpi esportati, non per ogni singolo corpo.
//
// COSA ERA GIA' PRESENTE IN v7:
// - NUOVO: se lo stesso componente compare piu' volte nell'assieme (es. 4 viti
//   identiche), prima veniva esportato UNA SOLA volta (deduplicato per nome).
//   Ora viene esportato UNA VOLTA PER OGNI OCCORRENZA: se compare 4 volte,
//   vengono scritti 4 file STL, con suffisso "_occNN" quando le occorrenze sono
//   piu' di una (es. "Vite_occ01.stl", "Vite_occ02.stl", ...). Se compare una
//   sola volta, il nome resta semplice ("Vite.stl"), come prima. Questo vale
//   sia la prima volta che l'assieme viene processato, sia quando lo script
//   riapre i .prt gia' noti dall'indice (v4): l'indice ora conserva le
//   occorrenze (non le deduplica piu').
// - NUOVO: tentativo BEST-EFFORT di separare corpi con piu' "lumps" (regioni
//   solide fisicamente disconnesse ma che appartengono allo stesso oggetto
//   Body di NX) prima di raccogliere/esportare i corpi di ogni parte. Questo
//   risolve il caso tipico in cui due solidi visivamente separati risultano
//   "fusi" in un unico file STL: non sono due Body diversi da esportare
//   separatamente, ma un solo Body con piu' lumps. L'operazione e' avvolta in
//   try/catch: se l'API non e' disponibile/compatibile con la tua versione di
//   NX, viene solo loggato un avviso e l'esportazione prosegue normalmente
//   (senza separare quel corpo). Se il problema persiste, il modo piu' sicuro
//   e' registrare un journal NX facendo manualmente "Separate Bodies" una
//   volta su un file che presenta il problema, e mandare il codice generato:
//   permette di adattare questa funzione con precisione alla tua versione NX.
// - NUOVO: log piu' leggibile, con una riga vuota prima di ogni componente
//   esportato (come nello screenshot fornito), e un riepilogo finale con i
//   TOTALI GENERALI (somma di tutti i corpi solidi esportati, di tutti i corpi
//   non chiusi esportati, e del numero totale di file STL scritti), oltre ai
//   conteggi per file STEP e per componente gia' presenti.
//
// COSA ERA GIA' PRESENTE (v6, v5, v4, v3, v2):
// - v6: i corpi NON solidi (superfici aperte/sheet) non vengono piu' scartati,
//   ma esportati in una sottocartella dedicata "000_Not_Closed_Mesh" dentro
//   l'output, con suffisso "_NOT_CLOSED_MESH" nel nome, senza alcuna ricucitura
//   automatica.
// - v5: un file STL per ogni corpo solido (niente piu' merge di corpi distinti
//   nello stesso file).
// - v4: se un assieme e' gia' stato processato in precedenza e i .prt dei
//   componenti esistono ancora in STEP_Convert, lo script apre direttamente
//   quei .prt invece di riaprire lo STEP (che altrimenti fallirebbe), e tiene
//   traccia della mappa step -> componenti nel file "component_index.txt".
// - v3: quando uno STEP e' un assieme senza corpi diretti, lo script scende
//   ricorsivamente nella struttura assembly (componenti gia' caricati in
//   sessione) ed esporta un file per ogni componente foglia con corpi.
// - v2: NXOpen.BasePart.CloseModified (enum annidato, non standalone), niente
//   LINQ, log sempre scritto su file anche in caso di errore.
//
// ISTRUZIONI D'USO:
// 1. In NX: Strumenti > Automazione > Journal > Riproduci... e seleziona
//    questo file .cs. Si apre subito una finestra vera (PowerShell): verifica
//    o modifica le cartelle di input (file STEP) e output (file STL) - di
//    default sono quelle configurate qui sotto - ed eventualmente apri
//    "Mostra opzioni avanzate" per cambiare le tolleranze STL o
//    l'esportazione delle superfici non chiuse solo per questa esecuzione,
//    poi premi "Avvia scansione".
// 2. La scansione confronta (senza aprire alcuna parte NX) cosa produrrebbe
//    il batch con quanto gia' presente nella cartella di output. Se non
//    trova conflitti si passa direttamente al punto 4, altrimenti appare una
//    seconda finestra con il riepilogo (Nuovo / OK / CONFLITTO per ogni file
//    STEP) e la scelta di come procedere.
// 3. Scegli come procedere: "Interrompi" (esce, nessun file scritto),
//    "Sovrascrivi" (procede, i conflitti rilevati vengono sovrascritti), o
//    "Copia in nuova cartella" (tutto l'output di questo run va in una nuova
//    sottocartella con timestamp, la cartella originale resta intatta).
// 4. Per ogni file .stp/.step:
//    - i corpi solidi diretti vengono esportati, un file per corpo;
//    - i corpi NON solidi (superfici aperte) diretti vengono esportati con
//      suffisso "_NOT_CLOSED_MESH";
//    - se una sorgente (lo STEP stesso, o un singolo componente di un
//      assieme) produce PIU' DI UN file totale (solidi + non chiusi), tutti
//      i suoi file vengono raggruppati in una sottocartella dedicata (i non
//      chiusi in una sotto-sottocartella "000_Not_Closed_Mesh" al suo
//      interno); se produce un solo file, resta piatto come prima;
//    - se non c'e' NESSUN corpo diretto (ne' solido ne' aperto) ed e' un
//      assieme, lo script scende nei sotto-componenti (o riapre i .prt gia'
//      noti dall'indice) e applica la stessa logica per ciascun componente,
//      esportando ogni occorrenza separatamente se il componente e' usato
//      piu' volte.
// 5. Il progresso della conversione si vede nella Listing Window di NX e nel
//    file di log, aggiornati riga per riga man mano che procede (nessun
//    pannello grafico dedicato: la GUI esterna serve solo per configurazione,
//    decisione e riepilogo finale). Per annullare a meta' batch, crea un file
//    CANCEL.txt nella cartella di output: viene rilevato al file STEP
//    successivo e cancellato automaticamente. Log dettagliato in
//    "log_conversione.txt", errori in "errori_conversione.log", mappa
//    step->componenti in "component_index.txt" (tutti dentro la cartella di
//    output effettiva di questo run).
// 6. Prova PRIMA su 2-3 file soli (includendo se possibile un assieme con un
//    componente ripetuto piu' volte, una parte con piu' corpi solidi, e/o
//    corpi multi-lump), poi lancia sul totale.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NXOpen;
using NXOpen.Assemblies;

public enum BatchDecision { Stop, Overwrite, CopyToNewFolder }

// Riga di riepilogo della scansione preventiva, per un singolo file STEP.
internal class ScanRow
{
    public string StepBaseName;
    public string Status;   // "Nuovo" / "OK" / "CONFLITTO"
    public string Detail;   // percorsi in collisione (se CONFLITTO), vuoto altrimenti
}

// Risultato completo della scansione preventiva su tutta la cartella di input.
internal class ScanSummary
{
    public int TotalSteps;
    public int NewCount;
    public int NoConflictCount;
    public int ConflictCount;
    public List<ScanRow> Rows = new List<ScanRow>();
}

public class NXJournal
{
    // =========================================================================
    // CONFIGURAZIONE (valori di default: modificabili anche dalla GUI ad ogni
    // esecuzione, senza dover editare questo file)
    // =========================================================================
    // internal (non piu' private): RunExternalGuiFlow/RunFallbackFlow
    // impostano questi campi con i valori scelti dall'utente subito prima di
    // avviare RunBatch.
    internal static string inputFolder = @"C:\Users\AndreaScalenghe\Desktop\STEP_Convert";

    // Cartella di output COME CONFIGURATA dall'utente (default o valore
    // inserito nella GUI). E' la base su cui viene calcolata "outputFolder",
    // la cartella EFFETTIVA di questo run (vedi RunBatch): in modalita'
    // "Sovrascrivi" coincidono, in modalita' "Copia in nuova cartella"
    // "outputFolder" diventa una sottocartella con timestamp sotto questa.
    internal static string configuredOutputFolder = @"C:\Users\AndreaScalenghe\Desktop\STL_Convert";

    // Cartella di output EFFETTIVA per il run in corso. Inizializzata uguale
    // a configuredOutputFolder (cosi' resta sensata anche se l'utente sceglie
    // "Interrompi" prima che RunBatch la ricalcoli), poi eventualmente
    // sovrascritta in RunBatch in base alla decisione scelta.
    internal static string outputFolder = configuredOutputFolder;

    // Tolleranze STL: valori di default, modificabili dal pannello "Opzioni
    // avanzate" della GUI esterna (vedi ApplyAdvancedOptions) subito prima di
    // ogni esecuzione. Non piu' readonly per questo motivo.
    internal static double chordalTol   = 0.0025;
    internal static double adjacencyTol = 0.08;
    internal static double angularTol   = 5.0;

    // true  = esporta anche i corpi NON solidi (superfici aperte/sheet) in una
    //         sottocartella dedicata dentro l'output, ben identificati nel nome
    // false = i corpi non solidi vengono ignorati
    // Anche questo modificabile dal pannello "Opzioni avanzate".
    internal static bool exportNotClosedMeshes = true;

    // Nome della sottocartella dove finiscono i corpi non chiusi: quando la
    // sorgente non e' raggruppata, e' direttamente dentro la cartella di
    // output; quando e' raggruppata (vedi ComputeExportFolders), e' annidata
    // dentro la sottocartella dedicata alla sorgente.
    internal static readonly string notClosedSubfolderName = "000_Not_Closed_Mesh";

    // Suffisso aggiunto al nome file dei corpi non chiusi, per riconoscerli subito
    private static readonly string notClosedSuffix = "_NOT_CLOSED_MESH";

    // DISATTIVATA per ora: la separazione automatica dei corpi multi-lump non
    // e' ancora implementata (il primo tentativo usava un'API NXOpen che non
    // esiste in questa versione di NX). Il flag e la funzione restano nel
    // codice, pronti per essere completati appena avremo il nome esatto
    // dell'API dal journal registrato (vedi commento sopra TrySeparateMultiLumpBodies).
    // Impostarlo a true non ha ancora alcun effetto.
    private static readonly bool trySeparateMultiLumpBodies = false;

    // true = la decisione "Sovrascrivi" scelta dall'utente nella GUI e' attiva
    // per il run corrente (impostato una volta all'inizio di RunBatch).
    private static bool allowOverwrite = false;

    // Percorsi assoluti gia' scritti in QUESTO run: usato per non sovrascrivere
    // mai silenziosamente un file appena prodotto da questo stesso batch,
    // indipendentemente dalla modalita' Sovrascrivi/Copia scelta.
    private static HashSet<string> writtenThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static List<string> logLines = new List<string>();

    // Percorso del file di log su cui scrivere in modo INCREMENTALE (una riga
    // alla volta, in append) man mano che l'esecuzione procede. Viene
    // impostato non appena la cartella di output effettiva e' nota (vedi
    // RunBatch); finche' resta null, Log() si limita ad accumulare in memoria
    // e a scrivere nella Listing Window, esattamente come prima.
    private static string logFilePath = null;

    // Cartella temporanea creata per lo scambio di file con il processo
    // powershell.exe della GUI esterna (vedi RunExternalGuiFlow), e percorso
    // dello script .ps1 scritto al suo interno. Restano valorizzati per
    // tutta la durata del run cosi' da poter mostrare anche la finestra di
    // riepilogo finale (TryShowExternalGuiSummary) con lo stesso script gia'
    // scritto, e per poter ripulire tutto a fine esecuzione
    // (CleanUpExternalGuiWorkDir). Null se la GUI esterna non e' mai partita.
    private static string externalGuiWorkDir = null;
    private static string externalGuiScriptPath = null;

    // Risultato di un'esecuzione di RunBatch: i conteggi erano gia' tutti
    // calcolati a fine metodo (variabili locali), qui vengono solo raccolti
    // in un oggetto cosi' che si possa mostrarne un riepilogo (nella GUI
    // esterna, se disponibile) invece che solo nella Listing Window.
    internal class BatchResult
    {
        public int TotalSteps;
        public int Ok;
        public int Failed;
        public int CompOk;
        public int CompFailed;
        public int GrandSolidBodies;
        public int GrandOpenBodies;
        public int GrandFiles;
        public int GrandSkippedFiles;
        public string OutputFolder;
        public string ErrorLogPath;
        public bool Cancelled;
        public string FatalError; // null = nessun errore fatale (non di singolo file)
    }

    // Applica le opzioni scelte nel pannello "Opzioni avanzate" della GUI
    // (o i valori di default, se il pannello non e' mai stato aperto) prima
    // di avviare una conversione.
    internal static void ApplyAdvancedOptions(double chordal, double adjacency, double angular, bool exportOpenBodies)
    {
        chordalTol = chordal;
        adjacencyTol = adjacency;
        angularTol = angular;
        exportNotClosedMeshes = exportOpenBodies;
    }

    public static void Main(string[] args)
    {
        Session theSession = Session.GetSession();
        ListingWindow lw = theSession.ListingWindow;
        lw.Open();

        Log(lw, "=== Avvio conversione batch STEP -> STL (v12) ===");

        // Percorso preferito: GUI vera mostrata da un processo powershell.exe
        // separato (vedi RunExternalGuiFlow) - configurazione cartelle,
        // eventuale risoluzione conflitti, e (a fine conversione) riepilogo.
        // Se PowerShell non fosse disponibile in questo ambiente per
        // qualunque motivo, il try/catch lo rileva e si passa
        // automaticamente al fallback a soli MessageBox/FolderBrowserDialog
        // (RunFallbackFlow), con la stessa identica logica "chiedi solo se
        // serve".
        BatchDecision decision = BatchDecision.Stop;
        bool externalGuiSucceeded = false;
        try
        {
            decision = RunExternalGuiFlow(lw);
            externalGuiSucceeded = true;
        }
        catch (Exception ex)
        {
            decision = BatchDecision.Stop;
            Log(lw, "GUI esterna (PowerShell) non disponibile in questo ambiente (" +
                ex.GetType().Name + ": " + ex.Message + "). Passo ai popup di sistema.");
        }

        if (!externalGuiSucceeded)
        {
            decision = RunFallbackFlow(lw);
        }

        outputFolder = configuredOutputFolder;

        if (decision == BatchDecision.Stop)
        {
            LogStopAndExit(lw, "Interrotto dall'utente prima di avviare la conversione. Nessun file scritto.");
            return;
        }

        BatchResult result = null;
        try
        {
            result = RunBatch(theSession, lw, decision);
        }
        catch (Exception ex)
        {
            Log(lw, "ERRORE GENERALE (lo script si e' fermato): " + ex.Message);
            Log(lw, ex.StackTrace);
        }
        finally
        {
            WriteFinalLogSafety();
        }

        if (externalGuiSucceeded && result != null)
        {
            TryShowExternalGuiSummary(lw, result);
        }

        CleanUpExternalGuiWorkDir();
    }

    // Script PowerShell della GUI esterna: un unico file con tre "stage"
    // (Config / Decision / Summary, scelti con il parametro -Stage), scritto
    // su disco una volta per run e rilanciato fino a 3 volte in processi
    // powershell.exe separati (vedi RunPowerShellStage). Ogni stage legge il
    // proprio file di input e scrive il proprio file di output dentro
    // -WorkDir, in un formato "chiave=valore" volutamente elementare (niente
    // libreria JSON necessaria su nessuno dei due lati). Tutti i numeri sono
    // sempre formattati/parsati con cultura invariante (punto come separatore
    // decimale), per non dipendere dalle impostazioni regionali della
    // macchina (es. virgola invece di punto con Windows in italiano).
    private static readonly string ExternalGuiScriptSource =
@"param(
    [Parameter(Mandatory=$true)][string]$Stage,
    [Parameter(Mandatory=$true)][string]$WorkDir
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ic = [System.Globalization.CultureInfo]::InvariantCulture

function Read-KeyValueFile($path) {
    $result = @{}
    if (Test-Path -LiteralPath $path) {
        Get-Content -LiteralPath $path -Encoding UTF8 | ForEach-Object {
            $line = $_
            $idx = $line.IndexOf('=')
            if ($idx -gt 0) {
                $key = $line.Substring(0, $idx)
                $val = $line.Substring($idx + 1)
                $result[$key] = $val
            }
        }
    }
    return $result
}

function Write-KeyValueFile($path, $dict) {
    $lines = @()
    foreach ($k in $dict.Keys) { $lines += (""{0}={1}"" -f $k, $dict[$k]) }
    Set-Content -LiteralPath $path -Value $lines -Encoding UTF8
}

function Parse-Double($text, $default) {
    $val = 0.0
    if ([double]::TryParse($text, [System.Globalization.NumberStyles]::Float, $ic, [ref]$val)) {
        return $val
    }
    return $default
}

switch ($Stage) {
    ""Config"" {
        $inputFile = Join-Path $WorkDir ""config_input.txt""
        $outputFile = Join-Path $WorkDir ""config_output.txt""
        $cfg = Read-KeyValueFile $inputFile

        $form = New-Object System.Windows.Forms.Form
        $form.Text = ""Conversione batch STEP -> STL""
        $form.Width = 640
        $form.Height = 440
        $form.StartPosition = ""CenterScreen""
        $form.FormBorderStyle = ""FixedDialog""
        $form.MinimizeBox = $false
        $form.MaximizeBox = $false
        $form.Topmost = $true

        $lblIn = New-Object System.Windows.Forms.Label
        $lblIn.Text = ""Cartella di input (file STEP):""
        $lblIn.SetBounds(20, 20, 580, 20)
        $form.Controls.Add($lblIn)

        $txtIn = New-Object System.Windows.Forms.TextBox
        $txtIn.SetBounds(20, 42, 500, 24)
        $txtIn.Text = $cfg[""InputFolder""]
        $form.Controls.Add($txtIn)

        $btnBrowseIn = New-Object System.Windows.Forms.Button
        $btnBrowseIn.Text = ""Sfoglia...""
        $btnBrowseIn.SetBounds(530, 41, 90, 26)
        $btnBrowseIn.Add_Click({
            $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
            if (Test-Path -LiteralPath $txtIn.Text) { $dlg.SelectedPath = $txtIn.Text }
            if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $txtIn.Text = $dlg.SelectedPath }
        })
        $form.Controls.Add($btnBrowseIn)

        $lblOut = New-Object System.Windows.Forms.Label
        $lblOut.Text = ""Cartella di output (file STL):""
        $lblOut.SetBounds(20, 80, 580, 20)
        $form.Controls.Add($lblOut)

        $txtOut = New-Object System.Windows.Forms.TextBox
        $txtOut.SetBounds(20, 102, 500, 24)
        $txtOut.Text = $cfg[""OutputFolder""]
        $form.Controls.Add($txtOut)

        $btnBrowseOut = New-Object System.Windows.Forms.Button
        $btnBrowseOut.Text = ""Sfoglia...""
        $btnBrowseOut.SetBounds(530, 101, 90, 26)
        $btnBrowseOut.Add_Click({
            $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
            if (Test-Path -LiteralPath $txtOut.Text) { $dlg.SelectedPath = $txtOut.Text }
            if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $txtOut.Text = $dlg.SelectedPath }
        })
        $form.Controls.Add($btnBrowseOut)

        $lblInfo = New-Object System.Windows.Forms.Label
        $lblInfo.Text = ""La scansione confronta i file STEP di input con gli STL gia' presenti in output, senza aprire NX. Se non trova conflitti la conversione parte subito.""
        $lblInfo.SetBounds(20, 136, 600, 40)
        $form.Controls.Add($lblInfo)

        $chkAdvanced = New-Object System.Windows.Forms.CheckBox
        $chkAdvanced.Text = ""Mostra opzioni avanzate (tolleranze STL, superfici non chiuse)""
        $chkAdvanced.SetBounds(20, 182, 420, 22)
        $form.Controls.Add($chkAdvanced)

        $panelAdv = New-Object System.Windows.Forms.Panel
        $panelAdv.SetBounds(20, 208, 600, 110)
        $panelAdv.Visible = $false
        $form.Controls.Add($panelAdv)

        $lblChordal = New-Object System.Windows.Forms.Label
        $lblChordal.Text = ""Tolleranza chordal:""
        $lblChordal.SetBounds(0, 4, 160, 20)
        $panelAdv.Controls.Add($lblChordal)

        $numChordal = New-Object System.Windows.Forms.NumericUpDown
        $numChordal.SetBounds(170, 2, 100, 22)
        $numChordal.DecimalPlaces = 4
        $numChordal.Increment = 0.0005
        $numChordal.Minimum = 0.0001
        $numChordal.Maximum = 10
        $numChordal.Value = [decimal](Parse-Double $cfg[""ChordalTol""] 0.0025)
        $panelAdv.Controls.Add($numChordal)

        $lblAdj = New-Object System.Windows.Forms.Label
        $lblAdj.Text = ""Tolleranza adjacency:""
        $lblAdj.SetBounds(0, 36, 160, 20)
        $panelAdv.Controls.Add($lblAdj)

        $numAdj = New-Object System.Windows.Forms.NumericUpDown
        $numAdj.SetBounds(170, 34, 100, 22)
        $numAdj.DecimalPlaces = 3
        $numAdj.Increment = 0.01
        $numAdj.Minimum = 0.001
        $numAdj.Maximum = 100
        $numAdj.Value = [decimal](Parse-Double $cfg[""AdjacencyTol""] 0.08)
        $panelAdv.Controls.Add($numAdj)

        $lblAng = New-Object System.Windows.Forms.Label
        $lblAng.Text = ""Tolleranza angular:""
        $lblAng.SetBounds(0, 68, 160, 20)
        $panelAdv.Controls.Add($lblAng)

        $numAng = New-Object System.Windows.Forms.NumericUpDown
        $numAng.SetBounds(170, 66, 100, 22)
        $numAng.DecimalPlaces = 1
        $numAng.Increment = 0.5
        $numAng.Minimum = 0.1
        $numAng.Maximum = 90
        $numAng.Value = [decimal](Parse-Double $cfg[""AngularTol""] 5.0)
        $panelAdv.Controls.Add($numAng)

        $chkExportOpen = New-Object System.Windows.Forms.CheckBox
        $chkExportOpen.Text = ""Esporta anche i corpi non chiusi (superfici aperte)""
        $chkExportOpen.SetBounds(0, 92, 460, 22)
        $chkExportOpen.Checked = ($cfg[""ExportNotClosed""] -ne ""0"")
        $panelAdv.Controls.Add($chkExportOpen)

        $chkAdvanced.Add_CheckedChanged({ $panelAdv.Visible = $chkAdvanced.Checked })

        $btnScan = New-Object System.Windows.Forms.Button
        $btnScan.Text = ""Avvia scansione""
        $btnScan.SetBounds(20, 350, 170, 36)
        $btnScan.DialogResult = [System.Windows.Forms.DialogResult]::OK
        $form.Controls.Add($btnScan)
        $form.AcceptButton = $btnScan

        $btnCancel = New-Object System.Windows.Forms.Button
        $btnCancel.Text = ""Annulla""
        $btnCancel.SetBounds(450, 350, 170, 36)
        $btnCancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
        $form.Controls.Add($btnCancel)
        $form.CancelButton = $btnCancel

        $result = $form.ShowDialog()

        $out = @{}
        if ($result -eq [System.Windows.Forms.DialogResult]::OK) {
            $out[""Cancelled""] = ""0""
            $out[""InputFolder""] = $txtIn.Text
            $out[""OutputFolder""] = $txtOut.Text
            $out[""ChordalTol""] = $numChordal.Value.ToString($ic)
            $out[""AdjacencyTol""] = $numAdj.Value.ToString($ic)
            $out[""AngularTol""] = $numAng.Value.ToString($ic)
            $out[""ExportNotClosed""] = if ($chkExportOpen.Checked) { ""1"" } else { ""0"" }
        } else {
            $out[""Cancelled""] = ""1""
        }
        Write-KeyValueFile $outputFile $out
    }
    ""Decision"" {
        $inputFile = Join-Path $WorkDir ""decision_input.txt""
        $outputFile = Join-Path $WorkDir ""decision_output.txt""
        $summaryText = """"
        if (Test-Path -LiteralPath $inputFile) {
            $summaryText = [string]::Join([Environment]::NewLine, (Get-Content -LiteralPath $inputFile -Encoding UTF8))
        }

        $form = New-Object System.Windows.Forms.Form
        $form.Text = ""Trovati conflitti - come procedere?""
        $form.Width = 640
        $form.Height = 480
        $form.StartPosition = ""CenterScreen""
        $form.FormBorderStyle = ""FixedDialog""
        $form.MinimizeBox = $false
        $form.MaximizeBox = $false
        $form.Topmost = $true

        $txtSummary = New-Object System.Windows.Forms.TextBox
        $txtSummary.Multiline = $true
        $txtSummary.ReadOnly = $true
        $txtSummary.ScrollBars = ""Vertical""
        $txtSummary.SetBounds(20, 20, 580, 330)
        $txtSummary.Text = $summaryText
        $form.Controls.Add($txtSummary)

        $btnOverwrite = New-Object System.Windows.Forms.Button
        $btnOverwrite.Text = ""Sovrascrivi""
        $btnOverwrite.SetBounds(20, 370, 175, 40)
        $form.Controls.Add($btnOverwrite)

        $btnCopy = New-Object System.Windows.Forms.Button
        $btnCopy.Text = ""Copia in nuova cartella""
        $btnCopy.SetBounds(215, 370, 195, 40)
        $form.Controls.Add($btnCopy)

        $btnStop = New-Object System.Windows.Forms.Button
        $btnStop.Text = ""Interrompi""
        $btnStop.SetBounds(430, 370, 170, 40)
        $form.Controls.Add($btnStop)

        $script:decision = ""Stop""
        $btnOverwrite.Add_Click({ $script:decision = ""Overwrite""; $form.Close() })
        $btnCopy.Add_Click({ $script:decision = ""Copy""; $form.Close() })
        $btnStop.Add_Click({ $script:decision = ""Stop""; $form.Close() })

        [void]$form.ShowDialog()

        Write-KeyValueFile $outputFile @{ ""Decision"" = $script:decision }
    }
    ""Summary"" {
        $inputFile = Join-Path $WorkDir ""summary_input.txt""
        $metaFile = Join-Path $WorkDir ""summary_meta.txt""
        $summaryText = """"
        if (Test-Path -LiteralPath $inputFile) {
            $summaryText = [string]::Join([Environment]::NewLine, (Get-Content -LiteralPath $inputFile -Encoding UTF8))
        }
        $meta = Read-KeyValueFile $metaFile
        $outFolder = $meta[""OutputFolder""]

        $form = New-Object System.Windows.Forms.Form
        $form.Text = ""Conversione completata""
        $form.Width = 600
        $form.Height = 420
        $form.StartPosition = ""CenterScreen""
        $form.FormBorderStyle = ""FixedDialog""
        $form.MinimizeBox = $false
        $form.MaximizeBox = $false
        $form.Topmost = $true

        $txt = New-Object System.Windows.Forms.TextBox
        $txt.Multiline = $true
        $txt.ReadOnly = $true
        $txt.ScrollBars = ""Vertical""
        $txt.SetBounds(20, 20, 540, 280)
        $txt.Text = $summaryText
        $form.Controls.Add($txt)

        $btnOpen = New-Object System.Windows.Forms.Button
        $btnOpen.Text = ""Apri cartella di output""
        $btnOpen.SetBounds(20, 320, 220, 38)
        $btnOpen.Add_Click({
            if (Test-Path -LiteralPath $outFolder) { Start-Process -FilePath ""explorer.exe"" -ArgumentList @($outFolder) }
        })
        $form.Controls.Add($btnOpen)

        $btnClose = New-Object System.Windows.Forms.Button
        $btnClose.Text = ""Chiudi""
        $btnClose.SetBounds(400, 320, 160, 38)
        $btnClose.Add_Click({ $form.Close() })
        $form.Controls.Add($btnClose)
        $form.AcceptButton = $btnClose

        [void]$form.ShowDialog()
    }
    default {
        Write-Error (""Stage sconosciuto: "" + $Stage)
        exit 1
    }
}
";

    // Percorso PRIMARIO: mostra la GUI vera lanciando powershell.exe come
    // processo SEPARATO da quello di NX (vedi il changelog v12 in cima al
    // file per il perche'). Gestisce, in ordine: schermata di configurazione
    // (cartelle + opzioni avanzate), scansione preventiva, ed EVENTUALMENTE
    // (solo se la scansione trova conflitti) la schermata di decisione.
    // Qualunque eccezione qui dentro (powershell.exe non trovato, processo
    // che non produce il file di output atteso, ecc.) risale a Main(), che
    // la intercetta e passa al fallback - stessa logica di degradazione
    // automatica e trasparente gia' in uso dalle versioni precedenti.
    private static BatchDecision RunExternalGuiFlow(ListingWindow lw)
    {
        string workDir = CreateExternalGuiWorkDir();
        string scriptPath = WriteExternalGuiScript(workDir);
        externalGuiWorkDir = workDir;
        externalGuiScriptPath = scriptPath;

        Dictionary<string, string> configInput = new Dictionary<string, string>();
        configInput["InputFolder"] = inputFolder;
        configInput["OutputFolder"] = configuredOutputFolder;
        configInput["ChordalTol"] = chordalTol.ToString(CultureInfo.InvariantCulture);
        configInput["AdjacencyTol"] = adjacencyTol.ToString(CultureInfo.InvariantCulture);
        configInput["AngularTol"] = angularTol.ToString(CultureInfo.InvariantCulture);
        configInput["ExportNotClosed"] = exportNotClosedMeshes ? "1" : "0";
        WriteKeyValueFile(Path.Combine(workDir, "config_input.txt"), configInput);

        RunPowerShellStage(scriptPath, workDir, "Config");
        Dictionary<string, string> configOutput = ReadKeyValueFile(Path.Combine(workDir, "config_output.txt"));

        if (configOutput.Count == 0 || GetFlag(configOutput, "Cancelled"))
        {
            Log(lw, "Interrotto dall'utente durante la configurazione. Nessun file scritto.");
            return BatchDecision.Stop;
        }

        string chosenInputFolder = GetOrDefault(configOutput, "InputFolder", inputFolder);
        string chosenOutputFolder = GetOrDefault(configOutput, "OutputFolder", configuredOutputFolder);

        if (!Directory.Exists(chosenInputFolder))
        {
            Log(lw, "ERRORE: cartella di input non trovata: " + chosenInputFolder);
            return BatchDecision.Stop;
        }
        inputFolder = chosenInputFolder;
        configuredOutputFolder = chosenOutputFolder;
        EnsureDirectory(configuredOutputFolder);

        ApplyAdvancedOptions(
            ParseInvariantDouble(configOutput, "ChordalTol", chordalTol),
            ParseInvariantDouble(configOutput, "AdjacencyTol", adjacencyTol),
            ParseInvariantDouble(configOutput, "AngularTol", angularTol),
            GetFlag(configOutput, "ExportNotClosed"));

        ScanSummary summary = PreScanConflicts(inputFolder, configuredOutputFolder);
        WriteScanSummaryFile(configuredOutputFolder, summary);

        if (summary.ConflictCount == 0)
        {
            Log(lw, string.Format(
                "Scansione: {0} file STEP, nessun conflitto rilevato. Procedo automaticamente, senza chiedere altro.",
                summary.TotalSteps));
            return BatchDecision.Overwrite;
        }

        File.WriteAllLines(Path.Combine(workDir, "decision_input.txt"), BuildScanSummaryLines(summary));

        RunPowerShellStage(scriptPath, workDir, "Decision");
        Dictionary<string, string> decisionOutput = ReadKeyValueFile(Path.Combine(workDir, "decision_output.txt"));
        string decisionStr = GetOrDefault(decisionOutput, "Decision", "Stop");

        if (decisionStr == "Overwrite")
        {
            return BatchDecision.Overwrite;
        }
        if (decisionStr == "Copy")
        {
            return BatchDecision.CopyToNewFolder;
        }

        Log(lw, "Interrotto dall'utente dopo la scansione preventiva. Nessun file scritto.");
        return BatchDecision.Stop;
    }

    // Best-effort: mostra il riepilogo finale nella GUI esterna (testo +
    // pulsante "Apri cartella di output"). Se qualcosa va storto qui la
    // conversione e' comunque gia' completata e il suo esito e' gia' nel log
    // e nella Listing Window: un fallimento in questo passo va solo loggato,
    // non deve mai far sembrare fallita la conversione stessa.
    private static void TryShowExternalGuiSummary(ListingWindow lw, BatchResult result)
    {
        if (string.IsNullOrEmpty(externalGuiWorkDir) || string.IsNullOrEmpty(externalGuiScriptPath))
        {
            return;
        }
        try
        {
            List<string> lines = new List<string>();
            if (!string.IsNullOrEmpty(result.FatalError))
            {
                lines.Add("ERRORE GENERALE: " + result.FatalError);
            }
            else if (result.Cancelled)
            {
                lines.Add("Conversione ANNULLATA dall'utente a meta' batch.");
            }
            else
            {
                lines.Add("Conversione completata.");
            }
            lines.Add("");
            lines.Add(string.Format("File STEP: {0} totali, {1} riusciti, {2} falliti.",
                result.TotalSteps, result.Ok, result.Failed));
            lines.Add(string.Format("Componenti da assiemi: {0} riusciti, {1} falliti.",
                result.CompOk, result.CompFailed));
            lines.Add(string.Format("File STL scritti: {0} ({1} corpi solidi, {2} corpi non chiusi).",
                result.GrandFiles, result.GrandSolidBodies, result.GrandOpenBodies));
            if (result.GrandSkippedFiles > 0)
            {
                lines.Add(string.Format("File saltati perche' gia' esistenti: {0}.", result.GrandSkippedFiles));
            }
            lines.Add("");
            lines.Add("Cartella di output: " + result.OutputFolder);

            File.WriteAllLines(Path.Combine(externalGuiWorkDir, "summary_input.txt"), lines.ToArray());

            Dictionary<string, string> meta = new Dictionary<string, string>();
            meta["OutputFolder"] = result.OutputFolder ?? "";
            WriteKeyValueFile(Path.Combine(externalGuiWorkDir, "summary_meta.txt"), meta);

            RunPowerShellStage(externalGuiScriptPath, externalGuiWorkDir, "Summary");
        }
        catch (Exception ex)
        {
            Log(lw, "Avviso: impossibile mostrare il riepilogo nella GUI esterna (" + ex.Message + ").");
        }
    }

    private static List<string> BuildScanSummaryLines(ScanSummary summary)
    {
        List<string> lines = new List<string>();
        lines.Add(string.Format(
            "Trovati {0} file STEP: {1} nuovi, {2} senza conflitti noti, {3} con conflitti rilevati.",
            summary.TotalSteps, summary.NewCount, summary.NoConflictCount, summary.ConflictCount));
        lines.Add("");
        lines.Add("File con output gia' esistente:");
        foreach (ScanRow row in summary.Rows)
        {
            if (row.Status == "CONFLITTO")
            {
                lines.Add("- " + row.StepBaseName + (string.IsNullOrEmpty(row.Detail) ? "" : " - " + row.Detail));
            }
        }
        lines.Add("");
        lines.Add("Elenco completo anche in ultima_scansione.txt, nella cartella di output.");
        lines.Add("");
        lines.Add("Nota: le sorgenti che generano piu' di un file totale (corpi solidi + superfici");
        lines.Add("aperte) verranno raggruppate automaticamente in una sottocartella dedicata.");
        return lines;
    }

    // Crea una cartella temporanea unica per questo run, sotto la cartella
    // temp dell'utente corrente (Path.GetTempPath, sempre scrivibile senza
    // diritti di amministratore). Un GUID nel nome evita collisioni tra run
    // concorrenti o file residui di un run precedente.
    private static string CreateExternalGuiWorkDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "nx_batch_stl_gui_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string WriteExternalGuiScript(string workDir)
    {
        string scriptPath = Path.Combine(workDir, "nx_batch_gui.ps1");
        File.WriteAllText(scriptPath, ExternalGuiScriptSource, new UTF8Encoding(false));
        return scriptPath;
    }

    // Lancia powershell.exe in attesa SINCRONA (WaitForExit): NXOpen non e'
    // thread-safe, quindi il journal deve comunque bloccarsi finche' l'utente
    // non ha finito con la finestra, esattamente come avrebbe fatto
    // Form.ShowDialog(). -WindowStyle Hidden + CreateNoWindow nascondono la
    // console di PowerShell (che qui non serve, e' solo un launcher): la
    // finestra WinForms creata dallo script rimane comunque visibile
    // normalmente, non essendo legata alla visibilita' della console.
    // -ExecutionPolicy Bypass vale solo per QUESTO singolo processo (non
    // cambia alcuna policy di sistema/utente) e non richiede diritti di
    // amministratore.
    private static void RunPowerShellStage(string scriptPath, string workDir, string stage)
    {
        string expectedOutputFile = null;
        if (stage == "Config")
        {
            expectedOutputFile = Path.Combine(workDir, "config_output.txt");
        }
        else if (stage == "Decision")
        {
            expectedOutputFile = Path.Combine(workDir, "decision_output.txt");
        }

        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "powershell.exe";
        psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" +
            scriptPath + "\" -Stage " + stage + " -WorkDir \"" + workDir + "\"";
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        using (Process process = Process.Start(psi))
        {
            process.WaitForExit();
        }

        if (expectedOutputFile != null && !File.Exists(expectedOutputFile))
        {
            throw new Exception("La GUI esterna non ha prodotto il file di risposta atteso per lo stage " + stage + ".");
        }
    }

    private static void WriteKeyValueFile(string path, Dictionary<string, string> data)
    {
        List<string> lines = new List<string>();
        foreach (KeyValuePair<string, string> kv in data)
        {
            lines.Add(kv.Key + "=" + kv.Value);
        }
        File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
    }

    private static Dictionary<string, string> ReadKeyValueFile(string path)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return result;
        }
        foreach (string rawLine in File.ReadAllLines(path))
        {
            int idx = rawLine.IndexOf('=');
            if (idx > 0)
            {
                result[rawLine.Substring(0, idx)] = rawLine.Substring(idx + 1);
            }
        }
        return result;
    }

    private static string GetOrDefault(Dictionary<string, string> data, string key, string defaultValue)
    {
        string val;
        if (data.TryGetValue(key, out val) && !string.IsNullOrEmpty(val))
        {
            return val;
        }
        return defaultValue;
    }

    private static bool GetFlag(Dictionary<string, string> data, string key)
    {
        string val;
        return data.TryGetValue(key, out val) && val == "1";
    }

    private static double ParseInvariantDouble(Dictionary<string, string> data, string key, double defaultValue)
    {
        string val;
        double parsed;
        if (data.TryGetValue(key, out val) &&
            double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }
        return defaultValue;
    }

    // Elimina la cartella temporanea di lavoro della GUI esterna a fine
    // esecuzione (script .ps1 e file di scambio inclusi). Best-effort: se
    // fallisce (es. antivirus che tiene un lock momentaneo) non e' un
    // problema, e' solo pulizia di file temporanei.
    private static void CleanUpExternalGuiWorkDir()
    {
        if (string.IsNullOrEmpty(externalGuiWorkDir))
        {
            return;
        }
        try
        {
            if (Directory.Exists(externalGuiWorkDir))
            {
                Directory.Delete(externalGuiWorkDir, true);
            }
        }
        catch (Exception)
        {
            // pulizia best-effort: file temporanei residui non sono un problema funzionale
        }
    }

    private static void LogStopAndExit(ListingWindow lw, string message)
    {
        Log(lw, message);
        WriteFinalLogSafety();
    }

    // Percorso di riserva, usato SOLO se la GUI esterna (PowerShell) non e'
    // utilizzabile in questo ambiente. Stessa logica "chiedi solo se serve"
    // della GUI vera: un solo selettore di cartella per input e uno per
    // output (nessuna domanda superflua "usare questa cartella?" - la
    // cartella di default e' gia' preselezionata nel selettore stesso), poi
    // la scansione, e infine UNA sola domanda finale - e solo se la
    // scansione ha davvero trovato dei conflitti da risolvere. Se non ci
    // sono conflitti si procede direttamente, senza altre interruzioni.
    private static BatchDecision RunFallbackFlow(ListingWindow lw)
    {
        string chosenInputFolder = AskForFolder("INPUT (i file .stp/.step da convertire)", inputFolder);
        if (chosenInputFolder == null)
        {
            Log(lw, "Interrotto dall'utente durante la scelta della cartella di input. Nessun file scritto.");
            return BatchDecision.Stop;
        }
        inputFolder = chosenInputFolder;

        string chosenOutputFolder = AskForFolder("OUTPUT (dove finiranno i file .stl)", configuredOutputFolder);
        if (chosenOutputFolder == null)
        {
            Log(lw, "Interrotto dall'utente durante la scelta della cartella di output. Nessun file scritto.");
            return BatchDecision.Stop;
        }
        configuredOutputFolder = chosenOutputFolder;

        if (!Directory.Exists(inputFolder))
        {
            MessageBox.Show("La cartella di input non esiste:\n" + inputFolder,
                "Cartella non trovata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log(lw, "ERRORE: cartella di input non trovata: " + inputFolder);
            return BatchDecision.Stop;
        }
        EnsureDirectory(configuredOutputFolder);

        ScanSummary summary;
        try
        {
            summary = PreScanConflicts(inputFolder, configuredOutputFolder);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Errore durante la scansione preventiva:\n" + ex.Message,
                "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Log(lw, "ERRORE durante la scansione preventiva: " + ex.Message);
            return BatchDecision.Stop;
        }

        WriteScanSummaryFile(configuredOutputFolder, summary);

        if (summary.ConflictCount == 0)
        {
            Log(lw, string.Format(
                "Scansione: {0} file STEP, nessun conflitto rilevato. Procedo automaticamente, senza chiedere altro.",
                summary.TotalSteps));
            AskExportNotClosedToggle(lw);
            return BatchDecision.Overwrite;
        }

        DialogResult modeResult = MessageBox.Show(
            BuildScanSummaryMessage(summary) +
            "\n\nSi' = SOVRASCRIVI i file in conflitto\n" +
            "No = COPIA tutto in una nuova cartella con data e ora (l'originale non viene toccato)\n" +
            "Annulla = Interrompi, nessun file scritto",
            "Trovati conflitti - come procedere?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

        if (modeResult == DialogResult.Cancel)
        {
            Log(lw, "Interrotto dall'utente dopo la scansione preventiva. Nessun file scritto.");
            return BatchDecision.Stop;
        }

        BatchDecision chosenDecision = (modeResult == DialogResult.Yes) ? BatchDecision.Overwrite : BatchDecision.CopyToNewFolder;
        AskExportNotClosedToggle(lw);
        return chosenDecision;
    }

    // Unica opzione avanzata raggiungibile dal fallback: il solo toggle
    // booleano per l'export dei corpi non chiusi. Le tolleranze numeriche
    // restano ai valori di default in questo percorso (opzione di nicchia,
    // per non introdurre componenti UI aggiuntivi non testati su questa
    // installazione, come Microsoft.VisualBasic.InputBox).
    private static void AskExportNotClosedToggle(ListingWindow lw)
    {
        DialogResult result = MessageBox.Show(
            "Esportare anche i corpi non chiusi (mesh aperte) in una sottocartella dedicata?\n\n" +
            "Si' = esporta (default)\nNo = salta i corpi non chiusi",
            "Corpi non chiusi", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        exportNotClosedMeshes = (result == DialogResult.Yes);
        Log(lw, "Export corpi non chiusi: " + (exportNotClosedMeshes ? "attivo" : "disattivato"));
    }

    // Selettore di cartella diretto: nessuna domanda preliminare "usare
    // quella di default?" - la cartella predefinita e' gia' preselezionata
    // nel dialogo stesso, quindi basta premere OK per confermarla cosi'
    // com'e', oppure navigare altrove prima di confermare. Annulla
    // interrompe. FolderBrowserDialog (a differenza di una Form
    // personalizzata) si appoggia al selettore di cartelle nativo di
    // Windows, quindi non risente del problema di compatibilita'
    // System.Drawing/System.Windows.Forms che colpisce Form.ShowDialog su
    // questa installazione NX - ma per sicurezza e' comunque avvolto in un
    // try/catch: se anche questo dovesse fallire, si ripiega sulla cartella
    // predefinita invece di far fallire tutto il journal.
    private static string AskForFolder(string folderDescription, string defaultFolder)
    {
        try
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Cartella di " + folderDescription;
                if (Directory.Exists(defaultFolder))
                {
                    dlg.SelectedPath = defaultFolder;
                }
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    return dlg.SelectedPath;
                }
                return null;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Non e' stato possibile aprire il selettore di cartelle in questo ambiente (" + ex.Message + ").\n" +
                "Verra' usata la cartella predefinita: " + defaultFolder,
                "Selettore non disponibile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return defaultFolder;
        }
    }

    // Riepilogo condensato per la MessageBox: conteggi piu' un elenco (troncato)
    // dei file in conflitto. Il dettaglio completo va sempre su file (vedi
    // WriteScanSummaryFile), per non affidarsi ai limiti pratici di una
    // MessageBox su batch con molti file.
    private static string BuildScanSummaryMessage(ScanSummary summary)
    {
        string text = string.Format(
            "Trovati {0} file STEP: {1} nuovi, {2} senza conflitti noti, {3} con conflitti rilevati.",
            summary.TotalSteps, summary.NewCount, summary.NoConflictCount, summary.ConflictCount);

        List<string> conflictNames = new List<string>();
        foreach (ScanRow row in summary.Rows)
        {
            if (row.Status == "CONFLITTO")
            {
                conflictNames.Add(row.StepBaseName);
            }
        }

        if (conflictNames.Count > 0)
        {
            text += "\n\nFile con output gia' esistente:\n";
            int shown = 0;
            foreach (string name in conflictNames)
            {
                if (shown >= 15)
                {
                    text += string.Format("... e altri {0} (elenco completo in ultima_scansione.txt)", conflictNames.Count - shown);
                    break;
                }
                text += "- " + name + "\n";
                shown++;
            }
        }

        text += "\n\nNota: le sorgenti che generano piu' di un file totale (corpi solidi + superfici\n" +
                "aperte) verranno raggruppate automaticamente in una sottocartella dedicata.";

        return text;
    }

    // Scrive SEMPRE il dettaglio completo della scansione su file (anche se
    // troncato nella MessageBox), cosi' su batch grandi non si perde nulla
    // solo perche' non ci sta nel popup.
    internal static void WriteScanSummaryFile(string outputFolderForScan, ScanSummary summary)
    {
        try
        {
            List<string> lines = new List<string>();
            lines.Add(string.Format("Scansione del {0}", DateTime.Now));
            lines.Add(string.Format("Trovati {0} file STEP: {1} nuovi, {2} senza conflitti noti, {3} con conflitti rilevati.",
                summary.TotalSteps, summary.NewCount, summary.NoConflictCount, summary.ConflictCount));
            lines.Add("");
            foreach (ScanRow row in summary.Rows)
            {
                string line = string.Format("[{0}] {1}", row.Status, row.StepBaseName);
                if (!string.IsNullOrEmpty(row.Detail))
                {
                    line += " - " + row.Detail;
                }
                lines.Add(line);
            }
            File.WriteAllLines(Path.Combine(outputFolderForScan, "ultima_scansione.txt"), lines.ToArray());
        }
        catch (Exception)
        {
            // file puramente informativo: se non si riesce a scrivere non blocchiamo il resto
        }
    }

    // Riscrive SEMPRE il log completo su file alla fine (anche se qualcosa e'
    // andato storto, o se l'utente ha scelto di interrompere prima di
    // iniziare), come rete di sicurezza aggiuntiva rispetto alla scrittura
    // incrementale che avviene durante il batch dentro RunBatch.
    private static void WriteFinalLogSafety()
    {
        try
        {
            string logDir = Directory.Exists(outputFolder) ? outputFolder : @"C:\Users\AndreaScalenghe\Desktop";
            string logFile = Path.Combine(logDir, "log_conversione.txt");
            File.WriteAllLines(logFile, logLines.ToArray());
        }
        catch (Exception)
        {
            // se anche questo fallisce non possiamo fare altro
        }
    }

    internal static BatchResult RunBatch(Session theSession, ListingWindow lw, BatchDecision decision)
    {
        if (!Directory.Exists(inputFolder))
        {
            Log(lw, "ERRORE: cartella di input non trovata: " + inputFolder);
            BatchResult notFoundResult = new BatchResult();
            notFoundResult.OutputFolder = outputFolder;
            notFoundResult.FatalError = "Cartella di input non trovata: " + inputFolder;
            return notFoundResult;
        }

        // Cartella EFFETTIVA di questo run: in modalita' "Copia in nuova
        // cartella" e' una sottocartella con timestamp sotto quella
        // configurata (mai toccata), altrimenti coincide con quella
        // configurata (comportamento identico alle versioni precedenti).
        outputFolder = (decision == BatchDecision.CopyToNewFolder)
            ? Path.Combine(configuredOutputFolder, "Export_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss"))
            : configuredOutputFolder;
        EnsureDirectory(outputFolder);

        allowOverwrite = (decision == BatchDecision.Overwrite);
        writtenThisRun.Clear();

        // Ora che la cartella di output effettiva esiste, si puo' iniziare a
        // scrivere il log in modo incrementale (vedi Log()). Il file viene
        // inizializzato con le righe gia' accumulate finora (es. il messaggio
        // di avvio), poi ogni chiamata a Log() vi appende una riga.
        logFilePath = Path.Combine(outputFolder, "log_conversione.txt");
        try
        {
            File.WriteAllLines(logFilePath, logLines.ToArray());
        }
        catch (Exception)
        {
            // se non riusciamo nemmeno ad azzerarlo, la scrittura incrementale
            // fallira' silenziosamente riga per riga; resta comunque la
            // scrittura finale di sicurezza in WriteFinalLogSafety().
        }

        if (decision == BatchDecision.CopyToNewFolder)
        {
            Log(lw, "Modalita' scelta: COPIA IN NUOVA CARTELLA. Tutto l'output di questo run va in: " + outputFolder);
        }
        else
        {
            Log(lw, "Modalita' scelta: SOVRASCRIVI. Gli output gia' esistenti rilevati dalla scansione verranno sovrascritti.");
        }

        string errLogPath = Path.Combine(outputFolder, "errori_conversione.log");
        string indexPath = GetComponentIndexPath();
        Dictionary<string, List<string>> componentIndex = LoadComponentIndex(indexPath);
        Dictionary<string, string> partNameOwner = BuildPartNameOwnerMap(componentIndex);

        if (componentIndex.Count > 0)
        {
            Log(lw, string.Format("Indice componenti caricato: {0} assiemi gia' noti da esecuzioni precedenti.",
                componentIndex.Count));
        }

        List<string> stepFiles = GetSortedStepFiles(inputFolder);

        Log(lw, string.Format("Trovati {0} file STEP da convertire in: {1}", stepFiles.Count, inputFolder));
        Log(lw, string.Format("Output STL in: {0}", outputFolder));

        // Meccanismo di annullamento cooperativo, valido indipendentemente da
        // quale GUI ha avviato il run: creando un file marker con questo nome
        // nella cartella di output (es. da Esplora risorse) durante
        // l'esecuzione, il batch si ferma pulito al file STEP successivo.
        string cancelMarkerPath = Path.Combine(outputFolder, "CANCEL.txt");
        Log(lw, "Per annullare durante l'esecuzione, crea un file di nome CANCEL.txt in: " + outputFolder);
        Log(lw, "");

        // Contatori file STEP (esportati come singolo STL con corpi diretti,
        // oppure come "assieme processato" se ha esportato almeno un componente)
        int ok = 0;
        int failed = 0;

        // Contatori componenti esportati singolarmente (da assiemi), una voce
        // per OGNI OCCORRENZA (quindi un componente x4 conta come 4, se tutte
        // e 4 le esportazioni riescono)
        int compOk = 0;
        int compFailed = 0;

        // Totali generali su tutta l'esecuzione: numero di corpi solidi
        // esportati, numero di corpi non chiusi esportati, numero totale di
        // file STL scritti (somma di entrambi, corpi diretti + componenti).
        int grandSolidBodies = 0;
        int grandOpenBodies = 0;
        int grandFiles = 0;

        // File STL che NON sono stati sovrascritti perche' gia' esistenti sul disco.
        int grandSkippedFiles = 0;

        bool cancelled = false;

        for (int i = 0; i < stepFiles.Count; i++)
        {
            if (File.Exists(cancelMarkerPath))
            {
                cancelled = true;
                Log(lw, "");
                Log(lw, string.Format("Annullato dall'utente: interrotto dopo {0} di {1} file STEP.", i, stepFiles.Count));
                try { if (File.Exists(cancelMarkerPath)) File.Delete(cancelMarkerPath); }
                catch (Exception) { /* marker gia' rimosso o non cancellabile: non blocca l'interruzione */ }
                break;
            }

            string stepFile = stepFiles[i];
            string baseName = Path.GetFileNameWithoutExtension(stepFile);
            Log(lw, string.Format("[{0}/{1}] {2}", i + 1, stepFiles.Count, baseName));

            // Controllo indice: se questo step e' gia' stato processato come assieme
            // in una precedente esecuzione, e i .prt dei suoi componenti esistono
            // ancora nella cartella di input, evito di riaprire lo STEP (fallirebbe
            // perche' NX trova gia' quei .prt) e apro direttamente i .prt registrati.
            // La lista puo' contenere lo stesso nome piu' volte: rappresenta le
            // occorrenze/quantita' di quel componente nell'assieme.
            List<string> knownComponents;
            bool useKnownComponents = false;
            if (componentIndex.TryGetValue(baseName, out knownComponents) && knownComponents.Count > 0)
            {
                useKnownComponents = true;
                HashSet<string> distinctNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string pn in knownComponents)
                {
                    distinctNames.Add(pn);
                }
                foreach (string pn in distinctNames)
                {
                    if (!File.Exists(Path.Combine(inputFolder, pn + ".prt")))
                    {
                        useKnownComponents = false;
                        break;
                    }
                }
            }

            if (useKnownComponents)
            {
                Log(lw, string.Format(
                    "  -> Assieme gia' aperto in precedenza: trovate {0} occorrenze di componenti gia' generati, li apro direttamente (salto riapertura STEP)...",
                    knownComponents.Count));

                // Conto quante volte compare ciascun nome, per poter assegnare
                // il suffisso "_occNN" solo quando serve (quantita' > 1).
                Dictionary<string, int> totalPerName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string pn in knownComponents)
                {
                    if (totalPerName.ContainsKey(pn))
                    {
                        totalPerName[pn]++;
                    }
                    else
                    {
                        totalPerName[pn] = 1;
                    }
                }

                Dictionary<string, int> occCounter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int compOkBefore = compOk;
                int compFailedBefore = compFailed;

                foreach (string partName in knownComponents)
                {
                    int total = totalPerName[partName];
                    int occ;
                    if (!occCounter.TryGetValue(partName, out occ))
                    {
                        occ = 0;
                    }
                    occ++;
                    occCounter[partName] = occ;

                    string fileBaseName = BuildInstanceFileBaseName(lw, baseName, partName, occ, total, partNameOwner);

                    ExportComponentPrtDirect(theSession, lw, partName, fileBaseName,
                        ref compOk, ref compFailed, errLogPath,
                        ref grandSolidBodies, ref grandOpenBodies, ref grandFiles, ref grandSkippedFiles);
                }

                int exportedHere = compOk - compOkBefore;
                int failedHere = compFailed - compFailedBefore;

                if (exportedHere > 0)
                {
                    ok++;
                }
                else
                {
                    failed++;
                }

                Log(lw, "");
                Log(lw, string.Format("  -> Assieme (da .prt esistenti) completato: {0} componenti esportati, {1} falliti",
                    exportedHere, failedHere));

                continue;
            }

            try
            {
                PartLoadStatus partLoadStatus1;
                BasePart basePart1 = theSession.Parts.OpenActiveDisplay(
                    stepFile, DisplayPartOption.AllowAdditional, out partLoadStatus1);
                DisposePartLoadStatus(partLoadStatus1);

                Part workPart = theSession.Parts.Work;
                theSession.ApplicationSwitchImmediate("UG_APP_MODELING");
                theSession.CleanUpFacetedFacesAndEdges();

                TrySeparateMultiLumpBodies(theSession, lw, workPart);

                List<Body> solidBodies = new List<Body>();
                List<Body> openBodies = new List<Body>();
                foreach (Body b in workPart.Bodies)
                {
                    if (b.IsSolidBody)
                    {
                        solidBodies.Add(b);
                    }
                    else
                    {
                        openBodies.Add(b);
                    }
                }

                int totalBodies = solidBodies.Count + openBodies.Count;

                if (totalBodies > 0)
                {
                    // Caso normale: corpi presenti direttamente nella parte principale.
                    // Ogni corpo va in un file STL separato, per non fondere corpi
                    // chiusi distinti in un'unica mesh non piu' separabile. Se la
                    // sorgente produce piu' di un file totale, tutto va raggruppato
                    // in una sottocartella dedicata (vedi ComputeExportFolders).
                    int effectiveOpenCount = exportNotClosedMeshes ? openBodies.Count : 0;
                    string solidTargetFolder, openTargetFolder;
                    ComputeExportFolders(outputFolder, baseName, solidBodies.Count, effectiveOpenCount,
                        out solidTargetFolder, out openTargetFolder);

                    int filesWritten = 0;

                    if (solidBodies.Count > 0)
                    {
                        List<string> outFiles = ExportBodiesSeparately(theSession, lw, solidBodies, solidTargetFolder,
                            baseName, ref grandSkippedFiles);
                        filesWritten += outFiles.Count;
                        grandSolidBodies += outFiles.Count;
                    }

                    if (exportNotClosedMeshes && openBodies.Count > 0)
                    {
                        List<string> outFilesOpen = ExportBodiesSeparately(theSession, lw, openBodies, openTargetFolder,
                            baseName + notClosedSuffix, ref grandSkippedFiles);
                        filesWritten += outFilesOpen.Count;
                        grandOpenBodies += outFilesOpen.Count;
                        Log(lw, string.Format("  -> Attenzione: {0} corpo/i non chiuso/i (superfici aperte), esportati in {1}\\",
                            openBodies.Count, notClosedSubfolderName));
                    }

                    if (solidBodies.Count + effectiveOpenCount > 1)
                    {
                        Log(lw, string.Format("  -> Output raggruppato in sottocartella: {0}\\", baseName));
                    }

                    grandFiles += filesWritten;
                    ok++;
                    Log(lw, string.Format("  -> OK ({0} corpi solidi, {1} non chiusi, {2} file totali)",
                        solidBodies.Count, openBodies.Count, filesWritten));
                }
                else
                {
                    // Nessun corpo diretto: probabile assieme, i corpi sono nei sotto-componenti.
                    Component root = null;
                    if (workPart.ComponentAssembly != null)
                    {
                        root = workPart.ComponentAssembly.RootComponent;
                    }

                    if (root == null)
                    {
                        throw new Exception("Nessun corpo trovato (ne' solido ne' superficie aperta) e nessuna struttura assembly presente.");
                    }

                    Log(lw, "  -> Nessun corpo diretto: rilevato assieme, esporto i sotto-componenti...");

                    // Prima passata: conto quante volte compare ciascun componente
                    // (per nome Part) nell'albero, cosi' so se serve il suffisso
                    // "_occNN" (quantita' > 1) oppure no.
                    Dictionary<string, int> totalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    CountLeafOccurrences(root, totalCounts);

                    Dictionary<string, int> exportedSoFar = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    int compOkBefore = compOk;
                    int compFailedBefore = compFailed;

                    ExportComponentTree(theSession, lw, root, totalCounts, exportedSoFar,
                        ref compOk, ref compFailed, errLogPath, baseName, indexPath, partNameOwner,
                        ref grandSolidBodies, ref grandOpenBodies, ref grandFiles, ref grandSkippedFiles);

                    int exportedHere = compOk - compOkBefore;
                    int failedHere = compFailed - compFailedBefore;

                    if (exportedHere == 0 && failedHere == 0)
                    {
                        throw new Exception("Assieme rilevato ma nessun componente con corpi trovato al suo interno.");
                    }

                    ok++;
                    Log(lw, "");
                    Log(lw, string.Format("  -> Assieme completato: {0} componenti esportati, {1} falliti",
                        exportedHere, failedHere));
                }
            }
            catch (Exception ex)
            {
                failed++;
                File.AppendAllText(errLogPath,
                    string.Format("{0} - {1} - {2}{3}", DateTime.Now, stepFile, ex.Message, Environment.NewLine));
                Log(lw, "  -> ERRORE: " + ex.Message);
            }
            finally
            {
                // Chiude tutte le parti aperte (compresi i componenti caricati) senza salvare,
                // prima di passare al file successivo.
                // NXOpen.BasePart.CloseModified e' un enum ANNIDATO dentro BasePart (non uno
                // standalone "BasePartCloseModified" - quello era l'errore nella v1).
                try
                {
                    theSession.Parts.CloseAll(NXOpen.BasePart.CloseModified.CloseModified, null);
                }
                catch (Exception exClose)
                {
                    Log(lw, "  -> Avviso: errore durante la chiusura della parte: " + exClose.Message);
                }
            }

            Log(lw, "");
        }

        Log(lw, "");
        Log(lw, "=====================================================");
        Log(lw, string.Format("File STEP: {0} riusciti, {1} falliti su {2} totali.", ok, failed, stepFiles.Count));
        Log(lw, string.Format("Componenti da assiemi: {0} esportati, {1} falliti.", compOk, compFailed));
        Log(lw, "");
        Log(lw, string.Format("Totale corpi solidi esportati: {0}", grandSolidBodies));
        Log(lw, string.Format("Totale corpi non chiusi esportati: {0}", grandOpenBodies));
        Log(lw, string.Format("Totale file STL scritti: {0}", grandFiles));
        if (grandSkippedFiles > 0)
        {
            Log(lw, string.Format("Totale file NON sovrascritti perche' gia' esistenti: {0}", grandSkippedFiles));
            Log(lw, "(dettaglio dei singoli file saltati sopra, nel log di questa esecuzione)");
        }
        if (failed > 0 || compFailed > 0)
        {
            Log(lw, "");
            Log(lw, "Dettagli errori in: " + errLogPath);
        }

        BatchResult result = new BatchResult();
        result.TotalSteps = stepFiles.Count;
        result.Ok = ok;
        result.Failed = failed;
        result.CompOk = compOk;
        result.CompFailed = compFailed;
        result.GrandSolidBodies = grandSolidBodies;
        result.GrandOpenBodies = grandOpenBodies;
        result.GrandFiles = grandFiles;
        result.GrandSkippedFiles = grandSkippedFiles;
        result.OutputFolder = outputFolder;
        result.ErrorLogPath = errLogPath;
        result.Cancelled = cancelled;
        return result;
    }

    // Elenco ordinato dei file .stp/.step in una cartella. Estratta a parte
    // cosi' sia RunBatch sia PreScanConflicts usano sempre la stessa identica
    // enumerazione (mai due implementazioni che potrebbero disallinearsi).
    private static List<string> GetSortedStepFiles(string folder)
    {
        List<string> stepFiles = new List<string>();
        stepFiles.AddRange(Directory.GetFiles(folder, "*.stp"));
        stepFiles.AddRange(Directory.GetFiles(folder, "*.step"));
        stepFiles.Sort();
        return stepFiles;
    }

    // Costruisce la mappa nome componente -> nome dello STEP che lo ha
    // "registrato" per primo, a partire dall'indice persistente gia' caricato.
    // Estratta a parte cosi' sia RunBatch sia PreScanConflicts partono sempre
    // dalla stessa logica.
    private static Dictionary<string, string> BuildPartNameOwnerMap(Dictionary<string, List<string>> componentIndex)
    {
        Dictionary<string, string> partNameOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<string>> entry in componentIndex)
        {
            foreach (string pn in entry.Value)
            {
                if (!partNameOwner.ContainsKey(pn))
                {
                    partNameOwner[pn] = entry.Key;
                }
            }
        }
        return partNameOwner;
    }

    private static void EnsureDirectory(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }

    // Decide le cartelle di destinazione per i corpi solidi e per i corpi non
    // chiusi di UNA sorgente (uno STEP con corpi diretti, o un singolo
    // componente di un assieme), applicando la regola di raggruppamento:
    // se il TOTALE dei file che questa sorgente produce (solidi + non chiusi)
    // e' maggiore di 1, tutto il suo output finisce in una sottocartella
    // dedicata (baseOutputFolder\fileBaseName\), con i non chiusi ulteriormente
    // annidati in una sotto-sottocartella (notClosedSubfolderName) al suo
    // interno. Se il totale e' 1, il comportamento resta piatto come nelle
    // versioni precedenti: il solido resta direttamente in baseOutputFolder,
    // oppure l'unico corpo non chiuso resta in baseOutputFolder\000_Not_Closed_Mesh\
    // (non annidato sotto nessuna sottocartella dedicata).
    private static void ComputeExportFolders(string baseOutputFolder, string fileBaseName,
        int solidCount, int openCount, out string solidTargetFolder, out string openTargetFolder)
    {
        bool grouped = (solidCount + openCount) > 1;
        string groupRoot = grouped ? Path.Combine(baseOutputFolder, fileBaseName) : baseOutputFolder;
        if (grouped)
        {
            EnsureDirectory(groupRoot);
        }

        solidTargetFolder = groupRoot;
        openTargetFolder = null;
        if (openCount > 0)
        {
            openTargetFolder = Path.Combine(groupRoot, notClosedSubfolderName);
            EnsureDirectory(openTargetFolder);
        }
    }

    // =========================================================================
    // SCANSIONE PREVENTIVA (nessuna sessione NX coinvolta - solo filesystem e
    // indice persistente, per restare veloce anche su centinaia di file)
    // =========================================================================

    // Determina, per ogni file STEP nella cartella di input, se e' "Nuovo"
    // (nessuna traccia ne' nell'indice ne' su disco - non puo' esserci
    // conflitto), "OK" (assieme gia' noto, nessun output coincidente
    // trovato), oppure "CONFLITTO" (esiste gia' almeno un output con lo
    // stesso nome/percorso previsto). Non apre alcuna parte in NX: per gli
    // assiemi gia' noti riusa la stessa logica di naming dell'export reale
    // (ResolveInstanceFileBaseName) contro una copia "scratch" della mappa
    // dei proprietari dei nomi, cosi' non tocca lo stato che user' il run
    // vero e proprio.
    internal static ScanSummary PreScanConflicts(string inputFolderToScan, string outputFolderToScan)
    {
        ScanSummary summary = new ScanSummary();

        List<string> stepFiles = GetSortedStepFiles(inputFolderToScan);
        summary.TotalSteps = stepFiles.Count;

        string indexPath = Path.Combine(outputFolderToScan, "component_index.txt");
        Dictionary<string, List<string>> componentIndex = LoadComponentIndex(indexPath);
        Dictionary<string, string> scratchOwner = BuildPartNameOwnerMap(componentIndex);

        foreach (string stepFile in stepFiles)
        {
            string baseName = Path.GetFileNameWithoutExtension(stepFile);
            List<string> knownComponents;
            bool isKnown = componentIndex.TryGetValue(baseName, out knownComponents) && knownComponents.Count > 0;

            List<string> hits = new List<string>();

            if (isKnown)
            {
                Dictionary<string, int> totalPerName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string pn in knownComponents)
                {
                    if (totalPerName.ContainsKey(pn))
                    {
                        totalPerName[pn]++;
                    }
                    else
                    {
                        totalPerName[pn] = 1;
                    }
                }

                Dictionary<string, int> occCounter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string partName in knownComponents)
                {
                    int total = totalPerName[partName];
                    int occ;
                    if (!occCounter.TryGetValue(partName, out occ))
                    {
                        occ = 0;
                    }
                    occ++;
                    occCounter[partName] = occ;

                    bool collision;
                    string owner;
                    string fileBaseName = ResolveInstanceFileBaseName(baseName, partName, occ, total, scratchOwner,
                        out collision, out owner);
                    hits.AddRange(FindExistingOutputsForBaseName(outputFolderToScan, fileBaseName));
                }
            }
            else
            {
                hits.AddRange(FindExistingOutputsForBaseName(outputFolderToScan, baseName));
            }

            string status;
            if (hits.Count > 0)
            {
                status = "CONFLITTO";
                summary.ConflictCount++;
            }
            else if (isKnown)
            {
                status = "OK";
                summary.NoConflictCount++;
            }
            else
            {
                status = "Nuovo";
                summary.NewCount++;
            }

            summary.Rows.Add(new ScanRow
            {
                StepBaseName = baseName,
                Status = status,
                Detail = hits.Count > 0 ? string.Join(", ", hits.ToArray()) : ""
            });
        }

        return summary;
    }

    // Verifica se esistono gia' output per "fileBaseName", controllando SIA la
    // convenzione piatta legacy (v8 e precedenti: file direttamente dentro
    // outputFolder) SIA quella raggruppata (v9: outputFolder\fileBaseName\...).
    // Controllare entrambe, sempre, permette di rilevare correttamente anche
    // gli output prodotti da versioni precedenti di questo journal come
    // conflitti, senza bisogno di logica speciale aggiuntiva.
    private static List<string> FindExistingOutputsForBaseName(string outputFolder, string fileBaseName)
    {
        List<string> hits = new List<string>();

        hits.AddRange(SafeGlob(outputFolder, fileBaseName + ".stl"));
        hits.AddRange(SafeGlob(outputFolder, fileBaseName + "_corpo??.stl"));
        string legacyNotClosed = Path.Combine(outputFolder, notClosedSubfolderName);
        hits.AddRange(SafeGlob(legacyNotClosed, fileBaseName + notClosedSuffix + "*.stl"));

        string grouped = Path.Combine(outputFolder, fileBaseName);
        if (Directory.Exists(grouped))
        {
            hits.AddRange(SafeGlob(grouped, fileBaseName + "*.stl"));
            string groupedNotClosed = Path.Combine(grouped, notClosedSubfolderName);
            hits.AddRange(SafeGlob(groupedNotClosed, fileBaseName + notClosedSuffix + "*.stl"));
        }

        return hits;
    }

    private static string[] SafeGlob(string folder, string pattern)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                return new string[0];
            }
            return Directory.GetFiles(folder, pattern);
        }
        catch (Exception)
        {
            return new string[0];
        }
    }

    // Costruisce il nome base del file per un componente, in base a quante
    // occorrenze totali ha nell'assieme: se una sola, nome semplice; se piu'
    // di una, aggiunge il suffisso "_occNN" (NN = indice dell'occorrenza).
    //
    // DISAMBIGUAZIONE TRA STEP DIVERSI: "partNameOwner" tiene traccia di quale
    // STEP (stepBaseName) ha usato per primo ciascun nome di componente. Se lo
    // stesso "partName" viene incontrato sotto uno STEP diverso da quello che
    // lo "possiede", si tratta di una collisione di naming tra assiemi
    // indipendenti (non della stessa parte ripetuta nello stesso assieme):
    // in questo caso il nome file viene prefissato con lo stepBaseName
    // corrente, per evitare che il file venga scambiato per un duplicato
    // gia' esportato e quindi saltato dalla protezione anti-sovrascrittura.
    // Nel caso comune (nessuna collisione) il nome resta semplice come prima.
    //
    // Funzione PURA (nessun logging, nessuna dipendenza da ListingWindow):
    // usata sia dall'export reale (tramite il wrapper BuildInstanceFileBaseName
    // sotto) sia dalla scansione preventiva, che le passa una mappa "scratch"
    // separata per non alterare lo stato del run vero e proprio.
    private static string ResolveInstanceFileBaseName(string stepBaseName, string partName,
        int occurrenceIndex, int totalOccurrences, Dictionary<string, string> partNameOwner,
        out bool isCollision, out string previousOwner)
    {
        string owner;
        isCollision = partNameOwner.TryGetValue(partName, out owner)
            && !string.Equals(owner, stepBaseName, StringComparison.OrdinalIgnoreCase);
        previousOwner = isCollision ? owner : null;

        if (!partNameOwner.ContainsKey(partName))
        {
            partNameOwner[partName] = stepBaseName;
        }

        string effectiveName = partName;
        if (isCollision)
        {
            effectiveName = stepBaseName + "_" + partName;
        }

        if (totalOccurrences <= 1)
        {
            return effectiveName;
        }
        return string.Format("{0}_occ{1:00}", effectiveName, occurrenceIndex);
    }

    // Wrapper con logging attorno a ResolveInstanceFileBaseName, usato dai
    // percorsi di export reale (RunBatch/ExportComponentTree/ExportComponentPrtDirect).
    private static string BuildInstanceFileBaseName(ListingWindow lw, string stepBaseName, string partName,
        int occurrenceIndex, int totalOccurrences, Dictionary<string, string> partNameOwner)
    {
        bool collision;
        string owner;
        string result = ResolveInstanceFileBaseName(stepBaseName, partName, occurrenceIndex, totalOccurrences,
            partNameOwner, out collision, out owner);

        if (collision)
        {
            Log(lw, string.Format(
                "     -> NOTA: il componente \"{0}\" e' gia' stato esportato per l'assieme \"{1}\": per evitare confusione questa occorrenza (da \"{2}\") viene rinominata in \"{3}\".",
                partName, owner, stepBaseName, result));
        }

        return result;
    }

    // Prima passata (sola lettura, nessun export): scorre ricorsivamente
    // l'albero dei componenti e conta quante volte compare ciascun nome di
    // Part tra i componenti CHE HANNO ALMENO UN CORPO PROPRIO. Serve per
    // sapere in anticipo la quantita' totale di ogni componente, cosi' la
    // seconda passata (ExportComponentTree) puo' assegnare correttamente i
    // suffissi "_occNN" fin dalla prima occorrenza incontrata.
    //
    // NOTA: un componente puo' avere SIA figli SIA corpi propri (es. un
    // sotto-assieme con lavorazioni/geometria aggiunta direttamente su di
    // esso). In quel caso non e' un componente "foglia" in senso stretto, ma
    // i suoi corpi propri vanno comunque contati: percio' dopo essere sceso
    // nei figli si prosegue SEMPRE a controllare anche il componente
    // corrente, invece di fermarsi (return) al solo fatto di avere figli.
    private static void CountLeafOccurrences(Component comp, Dictionary<string, int> counts)
    {
        Component[] children = comp.GetChildren();

        if (children != null && children.Length > 0)
        {
            foreach (Component child in children)
            {
                CountLeafOccurrences(child, counts);
            }
        }

        Part compPart = comp.Prototype as Part;
        if (compPart == null)
        {
            return;
        }

        bool hasBodies = false;
        foreach (Body b in compPart.Bodies)
        {
            hasBodies = true;
            break;
        }
        if (!hasBodies)
        {
            return;
        }

        string partName = compPart.Name;
        if (counts.ContainsKey(partName))
        {
            counts[partName]++;
        }
        else
        {
            counts[partName] = 1;
        }
    }

    // Scorre ricorsivamente l'albero dei componenti a partire da "comp".
    // Per ogni componente con corpi PROPRI, esporta un STL separato per ogni
    // OCCORRENZA (non deduplica piu' per nome: se lo stesso componente
    // compare 4 volte nell'assieme, viene esportato 4 volte, con suffisso
    // "_occNN"). Ogni occorrenza trovata viene anche registrata nell'indice
    // persistente (una riga per occorrenza, senza deduplica), cosi' alla
    // prossima esecuzione lo script sapra' sia quali componenti aprire sia in
    // che quantita'.
    //
    // NOTA: un componente puo' avere SIA figli SIA corpi propri (sotto-
    // assieme con geometria aggiuntiva applicata direttamente su di esso). Si
    // scende SEMPRE nei figli quando presenti, ma senza fermarsi li': si
    // controllano comunque anche i corpi propri del componente corrente,
    // invece di ignorarli in silenzio solo perche' non e' un nodo foglia.
    private static void ExportComponentTree(Session theSession, ListingWindow lw, Component comp,
        Dictionary<string, int> totalCounts, Dictionary<string, int> exportedSoFar,
        ref int compOk, ref int compFailed, string errLogPath,
        string stepBaseName, string indexPath, Dictionary<string, string> partNameOwner,
        ref int grandSolid, ref int grandOpen, ref int grandFiles, ref int grandSkipped)
    {
        Component[] children = comp.GetChildren();

        if (children != null && children.Length > 0)
        {
            foreach (Component child in children)
            {
                ExportComponentTree(theSession, lw, child, totalCounts, exportedSoFar,
                    ref compOk, ref compFailed, errLogPath, stepBaseName, indexPath, partNameOwner,
                    ref grandSolid, ref grandOpen, ref grandFiles, ref grandSkipped);
            }
        }

        // Prendo la Part reale (prototype) gia' caricata in sessione, per
        // controllare se questo componente ha anche corpi propri (che si
        // tratti di un vero nodo foglia o di un sotto-assieme con geometria
        // propria in aggiunta ai figli).
        Part compPart = comp.Prototype as Part;
        if (compPart == null)
        {
            return;
        }

        TrySeparateMultiLumpBodies(theSession, lw, compPart);

        List<Body> compSolidBodies = new List<Body>();
        List<Body> compOpenBodies = new List<Body>();
        foreach (Body b in compPart.Bodies)
        {
            if (b.IsSolidBody)
            {
                compSolidBodies.Add(b);
            }
            else
            {
                compOpenBodies.Add(b);
            }
        }

        int compTotalBodies = compSolidBodies.Count + compOpenBodies.Count;

        // Componente senza corpi (es. solo riferimento/geometria vuota): lo salto in silenzio.
        if (compTotalBodies == 0)
        {
            return;
        }

        string partName = compPart.Name;

        int total = totalCounts.ContainsKey(partName) ? totalCounts[partName] : 1;
        int occ;
        if (!exportedSoFar.TryGetValue(partName, out occ))
        {
            occ = 0;
        }
        occ++;
        exportedSoFar[partName] = occ;

        string fileBaseName = BuildInstanceFileBaseName(lw, stepBaseName, partName, occ, total, partNameOwner);

        // Registro questa occorrenza nell'indice: NX ha comunque generato/usato
        // un .prt per questa Part durante l'apertura dell'assieme, quindi vale la
        // pena tenerne traccia (con la quantita' corretta) anche se l'export
        // STL dovesse fallire.
        RegisterComponentOccurrenceInIndex(indexPath, stepBaseName, partName);

        Log(lw, "");

        try
        {
            // Ogni corpo in un file STL separato, per non fondere corpi chiusi
            // distinti in un'unica mesh non piu' separabile. I corpi non chiusi
            // (superfici aperte) finiscono nella sottocartella dedicata, e se il
            // componente e' raggruppato, ulteriormente annidata al suo interno.
            int effectiveOpenCount = exportNotClosedMeshes ? compOpenBodies.Count : 0;
            string solidTargetFolder, openTargetFolder;
            ComputeExportFolders(outputFolder, fileBaseName, compSolidBodies.Count, effectiveOpenCount,
                out solidTargetFolder, out openTargetFolder);

            int filesWritten = 0;

            if (compSolidBodies.Count > 0)
            {
                List<string> outFiles = ExportBodiesSeparately(theSession, lw, compSolidBodies, solidTargetFolder,
                    fileBaseName, ref grandSkipped);
                filesWritten += outFiles.Count;
                grandSolid += outFiles.Count;
            }

            if (exportNotClosedMeshes && compOpenBodies.Count > 0)
            {
                List<string> outFilesOpen = ExportBodiesSeparately(theSession, lw, compOpenBodies, openTargetFolder,
                    fileBaseName + notClosedSuffix, ref grandSkipped);
                filesWritten += outFilesOpen.Count;
                grandOpen += outFilesOpen.Count;
            }

            grandFiles += filesWritten;
            compOk++;

            string quantityNote = (total > 1) ? string.Format(" [istanza {0}/{1}]", occ, total) : "";
            string groupingNote = (compSolidBodies.Count + effectiveOpenCount > 1) ? " [raggruppato in sottocartella]" : "";
            Log(lw, string.Format("     -> componente OK: {0}{1}{2} ({3} solidi, {4} non chiusi, {5} file)",
                partName, quantityNote, groupingNote, compSolidBodies.Count, compOpenBodies.Count, filesWritten));
        }
        catch (Exception ex)
        {
            compFailed++;
            File.AppendAllText(errLogPath,
                string.Format("{0} - componente {1} (occorrenza {2}/{3}) - {4}{5}",
                    DateTime.Now, partName, occ, total, ex.Message, Environment.NewLine));
            Log(lw, string.Format("     -> componente ERRORE: {0} [istanza {1}/{2}] - {3}",
                partName, occ, total, ex.Message));
        }
    }

    // Apre direttamente un .prt di componente (gia' generato da NX in una
    // precedente apertura dell'assieme) ed esporta il relativo STL, senza
    // passare dallo STEP. "fileBaseName" e' gia' stato calcolato dal chiamante
    // (include il suffisso "_occNN" se il componente ha piu' occorrenze).
    private static void ExportComponentPrtDirect(Session theSession, ListingWindow lw, string partName, string fileBaseName,
        ref int compOk, ref int compFailed, string errLogPath,
        ref int grandSolid, ref int grandOpen, ref int grandFiles, ref int grandSkipped)
    {
        string prtPath = Path.Combine(inputFolder, partName + ".prt");

        Log(lw, "");

        try
        {
            if (!File.Exists(prtPath))
            {
                throw new Exception("File .prt non trovato: " + prtPath);
            }

            PartLoadStatus partLoadStatus1;
            BasePart basePart1 = theSession.Parts.OpenActiveDisplay(
                prtPath, DisplayPartOption.AllowAdditional, out partLoadStatus1);
            DisposePartLoadStatus(partLoadStatus1);

            Part workPart = theSession.Parts.Work;
            theSession.ApplicationSwitchImmediate("UG_APP_MODELING");
            theSession.CleanUpFacetedFacesAndEdges();

            TrySeparateMultiLumpBodies(theSession, lw, workPart);

            List<Body> solidBodies = new List<Body>();
            List<Body> openBodies = new List<Body>();
            foreach (Body b in workPart.Bodies)
            {
                if (b.IsSolidBody)
                {
                    solidBodies.Add(b);
                }
                else
                {
                    openBodies.Add(b);
                }
            }

            int totalBodies = solidBodies.Count + openBodies.Count;

            if (totalBodies == 0)
            {
                throw new Exception("Nessun corpo trovato nel componente (ne' solido ne' superficie aperta).");
            }

            // Ogni corpo in un file STL separato, per non fondere corpi chiusi
            // distinti in un'unica mesh non piu' separabile. I corpi non chiusi
            // (superfici aperte) finiscono nella sottocartella dedicata, e se il
            // componente e' raggruppato, ulteriormente annidata al suo interno.
            int effectiveOpenCount = exportNotClosedMeshes ? openBodies.Count : 0;
            string solidTargetFolder, openTargetFolder;
            ComputeExportFolders(outputFolder, fileBaseName, solidBodies.Count, effectiveOpenCount,
                out solidTargetFolder, out openTargetFolder);

            int filesWritten = 0;

            if (solidBodies.Count > 0)
            {
                List<string> outFiles = ExportBodiesSeparately(theSession, lw, solidBodies, solidTargetFolder,
                    fileBaseName, ref grandSkipped);
                filesWritten += outFiles.Count;
                grandSolid += outFiles.Count;
            }

            if (exportNotClosedMeshes && openBodies.Count > 0)
            {
                List<string> outFilesOpen = ExportBodiesSeparately(theSession, lw, openBodies, openTargetFolder,
                    fileBaseName + notClosedSuffix, ref grandSkipped);
                filesWritten += outFilesOpen.Count;
                grandOpen += outFilesOpen.Count;
            }

            grandFiles += filesWritten;
            compOk++;
            Log(lw, string.Format("     -> componente OK (da .prt esistente): {0} ({1} solidi, {2} non chiusi, {3} file)",
                fileBaseName, solidBodies.Count, openBodies.Count, filesWritten));
        }
        catch (Exception ex)
        {
            compFailed++;
            File.AppendAllText(errLogPath,
                string.Format("{0} - componente (prt diretto) {1} - {2}{3}", DateTime.Now, fileBaseName, ex.Message, Environment.NewLine));
            Log(lw, string.Format("     -> componente ERRORE (prt diretto): {0} - {1}", fileBaseName, ex.Message));
        }
        finally
        {
            try
            {
                theSession.Parts.CloseAll(NXOpen.BasePart.CloseModified.CloseModified, null);
            }
            catch (Exception)
            {
                // se anche la chiusura fallisce, si prosegue comunque con il componente successivo
            }
        }
    }

    // =========================================================================
    // SEPARAZIONE CORPI MULTI-LUMP (DISATTIVATA - in attesa dell'API corretta)
    // =========================================================================

    // ATTENZIONE: il tentativo iniziale usava NXOpen.Features.SeparateBodiesBuilder,
    // che pero' NON esiste in questa versione di NX (errore di compilazione).
    // La funzione e' stata quindi trasformata in uno STUB che non fa nulla:
    // i corpi multi-lump continueranno ad essere esportati cosi' come sono
    // (eventualmente uniti nello stesso file), finche' non integriamo l'API
    // corretta.
    //
    // Per risolvere in modo affidabile (senza altri tentativi "a indovinare"):
    // 1. In NX, apri manualmente un file con un corpo che ha piu' lumps
    //    (quello dove hai notato il problema).
    // 2. Strumenti > Automazione > Journal > Registra.
    // 3. Esegui manualmente il comando "Separate Bodies" (di solito sotto
    //    Home > Piu' / Inserisci > Combina, a seconda della versione/lingua)
    //    su quel corpo.
    // 4. Ferma la registrazione e mandami il file .cs generato: da li' prendo
    //    il nome esatto della classe/metodo NXOpen usati in questa versione
    //    di NX e completo questa funzione.
    private static void TrySeparateMultiLumpBodies(Session theSession, ListingWindow lw, Part part)
    {
        if (!trySeparateMultiLumpBodies || part == null)
        {
            return;
        }

        // Nessuna operazione per ora: la separazione multi-lump non e' attiva.
        // (Struttura lasciata pronta per l'integrazione futura dell'API corretta.)
    }

    // =========================================================================
    // INDICE PERSISTENTE step -> occorrenze componenti (file: component_index.txt)
    // Ogni riga e' "stepBaseName|partName". La STESSA coppia puo' comparire piu'
    // volte: il numero di righe uguali rappresenta la quantita' di quel
    // componente nell'assieme (NON viene piu' deduplicata).
    // =========================================================================

    private static string GetComponentIndexPath()
    {
        return Path.Combine(outputFolder, "component_index.txt");
    }

    private static Dictionary<string, List<string>> LoadComponentIndex(string path)
    {
        Dictionary<string, List<string>> index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return index;
        }

        string[] lines = File.ReadAllLines(path);
        foreach (string line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }
            string[] parts = line.Split('|');
            if (parts.Length != 2)
            {
                continue;
            }
            string step = parts[0].Trim();
            string comp = parts[1].Trim();
            if (step.Length == 0 || comp.Length == 0)
            {
                continue;
            }

            List<string> list;
            if (!index.TryGetValue(step, out list))
            {
                list = new List<string>();
                index[step] = list;
            }
            // NIENTE deduplica: ogni riga e' una occorrenza/quantita' distinta.
            list.Add(comp);
        }
        return index;
    }

    // Aggiunge SEMPRE una nuova riga per questa occorrenza (nessuna deduplica,
    // perche' la quantita' del componente e' significativa).
    private static void RegisterComponentOccurrenceInIndex(string indexPath, string stepBaseName, string partName)
    {
        try
        {
            File.AppendAllText(indexPath, stepBaseName + "|" + partName + Environment.NewLine);
        }
        catch (Exception)
        {
            // se la scrittura dell'indice fallisce non blocchiamo l'esportazione:
            // nel peggiore dei casi la prossima esecuzione non trovera' questa voce
            // e ritentera' l'apertura normale dello STEP.
        }
    }

    // Esporta una LISTA di corpi in file STL SEPARATI, uno per corpo, cosi' NX non
    // li fonde in un'unica mesh. Se c'e' un solo corpo, il file si chiama
    // "<baseFileName>.stl" (comportamento identico a prima). Se ce ne sono di
    // piu', ciascuno diventa "<baseFileName>_corpoNN.stl".
    // POLITICA DI SOVRASCRITTURA: se il file di destinazione esiste gia',
    // TryReserveOutputFile decide cosa fare in base alla modalita' scelta
    // dall'utente (allowOverwrite) e protegge sempre da collisioni interne
    // allo stesso run (vedi sotto). "skippedCount" viene incrementato per
    // ogni file effettivamente saltato.
    // Restituisce la lista dei percorsi file EFFETTIVAMENTE scritti (esclusi
    // quelli saltati).
    //
    // CleanUpFacetedFacesAndEdges() viene chiamata UNA SOLA VOLTA qui, dopo
    // aver esportato tutti i corpi di questo gruppo, invece che dopo ogni
    // singolo corpo: su parti con molti corpi evita chiamate ripetute inutili.
    private static List<string> ExportBodiesSeparately(Session theSession, ListingWindow lw, List<Body> bodies,
        string outputFolder, string baseFileName, ref int skippedCount)
    {
        List<string> outputFiles = new List<string>();

        if (bodies.Count == 0)
        {
            return outputFiles;
        }

        if (bodies.Count == 1)
        {
            string outFile = Path.Combine(outputFolder, baseFileName + ".stl");
            if (!TryReserveOutputFile(lw, outFile, ref skippedCount))
            {
                return outputFiles;
            }
            ExportSingleBodyToStl(theSession, bodies[0], outFile);
            outputFiles.Add(outFile);
            theSession.CleanUpFacetedFacesAndEdges();
            return outputFiles;
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            string outFile = Path.Combine(outputFolder, string.Format("{0}_corpo{1:00}.stl", baseFileName, i + 1));
            if (!TryReserveOutputFile(lw, outFile, ref skippedCount))
            {
                continue;
            }
            ExportSingleBodyToStl(theSession, bodies[i], outFile);
            outputFiles.Add(outFile);
        }

        if (outputFiles.Count > 0)
        {
            theSession.CleanUpFacetedFacesAndEdges();
        }

        return outputFiles;
    }

    // Decide se un file di output puo' essere scritto in "outFile":
    // - se e' gia' stato scritto IN QUESTO STESSO RUN (writtenThisRun), e'
    //   una collisione interna tra due sorgenti diverse: viene sempre
    //   saltata con avviso, indipendentemente dalla modalita' scelta, per
    //   non sovrascrivere mai silenziosamente un file appena prodotto da
    //   questo batch.
    // - altrimenti, se esiste gia' da un run PRECEDENTE: viene sovrascritto
    //   solo se l'utente ha scelto "Sovrascrivi" (allowOverwrite); altrimenti
    //   saltato con log "SKIP", esattamente come in v8.
    // In ogni caso in cui l'export procede, il percorso viene registrato in
    // writtenThisRun.
    private static bool TryReserveOutputFile(ListingWindow lw, string outFile, ref int skippedCount)
    {
        if (File.Exists(outFile))
        {
            if (writtenThisRun.Contains(outFile))
            {
                skippedCount++;
                Log(lw, string.Format(
                    "     -> ATTENZIONE: collisione interna in questo stesso run per \"{0}\": corpo saltato per non sovrascrivere un file appena scritto.",
                    Path.GetFileName(outFile)));
                return false;
            }

            if (!allowOverwrite)
            {
                skippedCount++;
                Log(lw, string.Format("     -> SKIP: \"{0}\" esiste gia' in {1}, NON sovrascritto (corpo saltato).",
                    Path.GetFileName(outFile), Path.GetDirectoryName(outFile)));
                return false;
            }

            Log(lw, string.Format("     -> OVERWRITE: \"{0}\" gia' esistente in {1}, sovrascritto (scelta dell'utente).",
                Path.GetFileName(outFile), Path.GetDirectoryName(outFile)));
        }

        writtenThisRun.Add(outFile);
        return true;
    }

    // Esporta UN SOLO corpo in un file STL dedicato.
    // STLCreator.Destroy() e' in un blocco finally: se Commit() lancia
    // un'eccezione (es. corpo non valido, path non scrivibile), la risorsa NX
    // viene comunque rilasciata invece di restare aperta per il resto del batch.
    private static void ExportSingleBodyToStl(Session theSession, Body body, string outputFile)
    {
        STLCreator stlCreator1 = theSession.DexManager.CreateStlCreator();
        try
        {
            stlCreator1.AutoNormalGen = true;
            stlCreator1.ChordalTol = chordalTol;
            stlCreator1.AdjacencyTol = adjacencyTol;
            stlCreator1.AngularTol = angularTol;
            stlCreator1.OutputFile = outputFile;

            NXObject[] singleBodyArray = new NXObject[] { body };
            stlCreator1.ExportSelectionBlock.Add(singleBodyArray);

            stlCreator1.Commit();
        }
        finally
        {
            stlCreator1.Destroy();
        }
    }

    // Rilascia in modo sicuro un PartLoadStatus: se Dispose() stesso dovesse
    // lanciare un'eccezione (o l'oggetto fosse null), non deve mascherare o
    // interrompere il flusso principale del journal.
    private static void DisposePartLoadStatus(PartLoadStatus partLoadStatus)
    {
        if (partLoadStatus == null)
        {
            return;
        }
        try
        {
            partLoadStatus.Dispose();
        }
        catch (Exception)
        {
            // ignorato: non e' una risorsa critica da far fallire il resto dell'export
        }
    }

    internal static void Log(ListingWindow lw, string message)
    {
        logLines.Add(message);
        try
        {
            lw.WriteLine(message);
        }
        catch (Exception)
        {
            // se la Listing Window non e' disponibile, continuiamo comunque:
            // il messaggio resta salvato in logLines e finira' nel file di log.
        }

        // Scrittura incrementale: se il percorso del log e' gia' noto (la
        // cartella di output effettiva esiste), appendo subito questa riga su
        // disco, cosi' il log resta leggibile anche se il journal viene
        // interrotto a meta' (crash di NX, chiusura forzata, ecc.) e non si
        // arriva mai alla scrittura finale in WriteFinalLogSafety().
        if (logFilePath != null)
        {
            try
            {
                File.AppendAllText(logFilePath, message + Environment.NewLine);
            }
            catch (Exception)
            {
                // se anche l'append fallisce, il messaggio resta comunque in
                // logLines e verra' ritentato nella scrittura finale.
            }
        }

    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
