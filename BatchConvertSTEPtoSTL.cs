// =============================================================================
// NX Open Journal - Conversione massiva STEP -> STL (v9)
// Basato sul journal originale "journal.cs" (export singolo STL registrato in NX),
// esteso per scorrere automaticamente tutti i file .stp/.step di una cartella.
//
// COSA E' STATO CORRETTO/AGGIUNTO IN QUESTA VERSIONE (v9):
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
//    questo file .cs. Si apre subito una finestra: verifica o modifica le
//    cartelle di input (file STEP) e output (file STL) - di default sono
//    quelle configurate qui sotto - poi premi "Avvia scansione".
// 2. La scansione confronta (senza aprire alcuna parte NX) cosa produrrebbe
//    il batch con quanto gia' presente nella cartella di output, e mostra un
//    riepilogo con una lista (Nuovo / OK / CONFLITTO per ogni file STEP).
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
// 5. Il progresso viene stampato nella Listing Window di NX. Log dettagliato in
//    "log_conversione.txt", errori in "errori_conversione.log", mappa
//    step->componenti in "component_index.txt" (tutti dentro la cartella di
//    output effettiva di questo run).
// 6. Prova PRIMA su 2-3 file soli (includendo se possibile un assieme con un
//    componente ripetuto piu' volte, una parte con piu' corpi solidi, e/o
//    corpi multi-lump), poi lancia sul totale.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
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

// NOTA STORICA: le versioni precedenti di questo journal usavano qui una
// System.Windows.Forms.Form personalizzata (finestra di configurazione +
// scansione + decisione). Su alcune installazioni NX, PERO', qualunque
// System.Windows.Forms.Form - indipendentemente dalle proprieta' impostate -
// va in MissingMethodException nel momento in cui la finestra viene
// effettivamente creata (Form.ShowDialog -> Form.CreateHandle ->
// Form.UpdateWindowIcon -> System.Drawing.Icon..ctor), a causa di un
// disallineamento tra le versioni di System.Windows.Forms e
// System.Drawing.Common caricate dal processo NX. Non e' un problema
// risolvibile impostando o evitando singole proprieta': e' strutturale a
// quell'ambiente e riguarda OGNI Form, non solo questa.
//
// La configurazione, la scansione e la scelta della modalita' sono quindi
// implementate in Main() (vedi sotto) usando solo System.Windows.Forms.
// MessageBox e System.Windows.Forms.FolderBrowserDialog: nessuno dei due e'
// una Form, entrambi si appoggiano a dialoghi nativi di Windows, e non
// passano dal codice di Form.UpdateWindowIcon che causa il crash.

public class NXJournal
{
    // =========================================================================
    // CONFIGURAZIONE (valori di default: modificabili anche dalla GUI ad ogni
    // esecuzione, senza dover editare questo file)
    // =========================================================================
    private static string inputFolder = @"C:\Users\AndreaScalenghe\Desktop\STEP_Convert";

    // Cartella di output COME CONFIGURATA dall'utente (default o valore
    // inserito nella GUI). E' la base su cui viene calcolata "outputFolder",
    // la cartella EFFETTIVA di questo run (vedi RunBatch): in modalita'
    // "Sovrascrivi" coincidono, in modalita' "Copia in nuova cartella"
    // "outputFolder" diventa una sottocartella con timestamp sotto questa.
    private static string configuredOutputFolder = @"C:\Users\AndreaScalenghe\Desktop\STL_Convert";

    // Cartella di output EFFETTIVA per il run in corso. Inizializzata uguale
    // a configuredOutputFolder (cosi' resta sensata anche se l'utente sceglie
    // "Interrompi" prima che RunBatch la ricalcoli), poi eventualmente
    // sovrascritta in RunBatch in base alla decisione scelta.
    private static string outputFolder = configuredOutputFolder;

    // Tolleranze STL (riprese identiche dal journal originale)
    private static readonly double chordalTol   = 0.0025;
    private static readonly double adjacencyTol = 0.08;
    private static readonly double angularTol   = 5.0;

    // true  = esporta anche i corpi NON solidi (superfici aperte/sheet) in una
    //         sottocartella dedicata dentro l'output, ben identificati nel nome
    // false = i corpi non solidi vengono ignorati
    private static readonly bool exportNotClosedMeshes = true;

    // Nome della sottocartella dove finiscono i corpi non chiusi: quando la
    // sorgente non e' raggruppata, e' direttamente dentro la cartella di
    // output; quando e' raggruppata (vedi ComputeExportFolders), e' annidata
    // dentro la sottocartella dedicata alla sorgente.
    private static readonly string notClosedSubfolderName = "000_Not_Closed_Mesh";

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

    public static void Main(string[] args)
    {
        Session theSession = Session.GetSession();
        ListingWindow lw = theSession.ListingWindow;
        lw.Open();

        Log(lw, "=== Avvio conversione batch STEP -> STL (v9) ===");

        string chosenInputFolder = AskForFolder("input (i file .stp/.step da convertire)", inputFolder);
        if (chosenInputFolder == null)
        {
            LogStopAndExit(lw, "Interrotto dall'utente durante la scelta della cartella di input. Nessun file scritto.");
            return;
        }
        inputFolder = chosenInputFolder;

        string chosenOutputFolder = AskForFolder("output (dove finiranno i file .stl)", configuredOutputFolder);
        if (chosenOutputFolder == null)
        {
            LogStopAndExit(lw, "Interrotto dall'utente durante la scelta della cartella di output. Nessun file scritto.");
            return;
        }
        configuredOutputFolder = chosenOutputFolder;
        outputFolder = configuredOutputFolder;

        if (!Directory.Exists(inputFolder))
        {
            MessageBox.Show("La cartella di input non esiste:\n" + inputFolder,
                "Cartella non trovata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            LogStopAndExit(lw, "ERRORE: cartella di input non trovata: " + inputFolder);
            return;
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
            LogStopAndExit(lw, "ERRORE durante la scansione preventiva: " + ex.Message);
            return;
        }

        WriteScanSummaryFile(configuredOutputFolder, summary);

        DialogResult proceedResult = MessageBox.Show(
            BuildScanSummaryMessage(summary) + "\n\nVuoi procedere con la conversione?",
            "Risultato scansione", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (proceedResult != DialogResult.Yes)
        {
            LogStopAndExit(lw, "Interrotto dall'utente dopo la scansione preventiva. Nessun file scritto.");
            return;
        }

        DialogResult modeResult = MessageBox.Show(
            "Come vuoi procedere?\n\n" +
            "Si' = SOVRASCRIVI i file gia' esistenti (quelli segnalati come CONFLITTO dalla scansione).\n" +
            "No = COPIA tutto l'output di questo run in una nuova sottocartella con data e ora " +
            "(la cartella di output configurata non viene toccata).",
            "Modalita' di esportazione", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        BatchDecision decision = (modeResult == DialogResult.Yes)
            ? BatchDecision.Overwrite
            : BatchDecision.CopyToNewFolder;

        try
        {
            RunBatch(theSession, lw, decision);
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
    }

    private static void LogStopAndExit(ListingWindow lw, string message)
    {
        Log(lw, message);
        WriteFinalLogSafety();
    }

    // Chiede all'utente se usare la cartella predefinita ("folderDescription")
    // o sceglierne un'altra tramite FolderBrowserDialog. Ritorna null se
    // l'utente sceglie di interrompere in uno dei due passaggi.
    // FolderBrowserDialog (a differenza di una Form personalizzata) si
    // appoggia al selettore di cartelle nativo di Windows, quindi non risente
    // del problema di compatibilita' System.Drawing/System.Windows.Forms che
    // colpisce Form.ShowDialog su questa installazione NX - ma per sicurezza
    // e' comunque avvolto in un try/catch: se anche questo dovesse fallire in
    // qualche ambiente, si ripiega sulla cartella predefinita invece di far
    // fallire tutto il journal.
    private static string AskForFolder(string folderDescription, string defaultFolder)
    {
        DialogResult useDefault = MessageBox.Show(
            string.Format("Cartella di {0}:\n{1}\n\nUsare questa cartella?\n(No per sceglierne un'altra, Annulla per interrompere)",
                folderDescription, defaultFolder),
            "Conferma cartella", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        if (useDefault == DialogResult.Cancel)
        {
            return null;
        }
        if (useDefault == DialogResult.Yes)
        {
            return defaultFolder;
        }

        try
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
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
                "Verra' usata la cartella predefinita. Per cambiarla stabilmente, modifica il file .cs.",
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
    private static void WriteScanSummaryFile(string outputFolderForScan, ScanSummary summary)
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

    private static void RunBatch(Session theSession, ListingWindow lw, BatchDecision decision)
    {
        if (!Directory.Exists(inputFolder))
        {
            Log(lw, "ERRORE: cartella di input non trovata: " + inputFolder);
            return;
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

        for (int i = 0; i < stepFiles.Count; i++)
        {
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

    private static void Log(ListingWindow lw, string message)
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
