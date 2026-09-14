// =============================================================================
// NX Open Journal - Conversione massiva STEP -> STL (v11)
// Basato sul journal originale "journal.cs" (export singolo STL registrato in NX),
// esteso per scorrere automaticamente tutti i file .stp/.step di una cartella.
//
// COSA E' STATO CORRETTO/AGGIUNTO IN QUESTA VERSIONE (v11):
// - CONFERMATO su questa installazione NX, anche a sessione appena riavviata e
//   con la mitigazione TryPreloadCompatibleSystemDrawing gia' attiva: una
//   Form vera NON PUO' aprirsi (stesso MissingMethodException su
//   System.Drawing.Icon..ctor visto fin dal primo test). Il problema e'
//   strutturale a questa installazione, non risolvibile da un journal .cs.
//   Il percorso di fallback (MessageBox/FolderBrowserDialog) e' quindi
//   diventato il percorso primario atteso, non piu' un ripiego di emergenza;
//   di conseguenza gli sono state portate le funzionalita' che prima
//   esistevano solo dentro LauncherForm:
//   - Annullamento a meta' batch anche senza bottone "Annulla": creando un
//     file di nome CANCEL.txt nella cartella di output durante l'esecuzione,
//     il batch si ferma in modo pulito al file STEP successivo (il journal
//     lo dice esplicitamente nel log all'avvio, e cancella da solo il file
//     marker una volta rilevato).
//   - Domanda opzionale (un solo MessageBox Si'/No) per decidere se
//     esportare anche i corpi non chiusi in questa esecuzione, con la stessa
//     logica "chiedi solo se serve" del resto del flusso (le tolleranze STL
//     numeriche restano ai valori di default in questo percorso).
//   Il contatore di avanzamento "[i/totale]" nel log era gia' presente dalla
//   v8 in poi (Log() lo scrive ad ogni file), quindi era gia' visibile anche
//   nel fallback: nessuna modifica necessaria per quello.
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
//    questo file .cs. Si apre subito una finestra: verifica o modifica le
//    cartelle di input (file STEP) e output (file STL) - di default sono
//    quelle configurate qui sotto - ed eventualmente apri "Mostra opzioni
//    avanzate" per cambiare le tolleranze STL o l'esportazione delle
//    superfici non chiuse solo per questa esecuzione, poi premi
//    "Avvia scansione".
// 2. La scansione confronta (senza aprire alcuna parte NX) cosa produrrebbe
//    il batch con quanto gia' presente nella cartella di output, e mostra un
//    riepilogo con una lista (Nuovo / OK / CONFLITTO per ogni file STEP).
// 3. Scegli come procedere: "Interrompi" (esce, nessun file scritto),
//    "Sovrascrivi" (procede, i conflitti rilevati vengono sovrascritti), o
//    "Copia in nuova cartella" (tutto l'output di questo run va in una nuova
//    sottocartella con timestamp, la cartella originale resta intatta). La
//    stessa finestra passa quindi al pannello di avanzamento (barra di
//    progresso, file corrente, log in tempo reale, bottone "Annulla") e
//    infine al riepilogo finale, senza mai chiudersi nel frattempo.
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
// 5. Il progresso si vede in tempo reale nel pannello della finestra stessa
//    (oltre che nella Listing Window di NX, che resta comunque aggiornata).
//    Log dettagliato in "log_conversione.txt", errori in
//    "errori_conversione.log", mappa step->componenti in
//    "component_index.txt" (tutti dentro la cartella di output effettiva di
//    questo run).
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

// ATTENZIONE - compatibilita': su alcune installazioni NX, QUALUNQUE
// System.Windows.Forms.Form (indipendentemente dalle proprieta' impostate)
// va in MissingMethodException nel momento in cui la finestra viene
// effettivamente creata (Form.ShowDialog -> Form.CreateHandle ->
// Form.UpdateWindowIcon -> System.Drawing.Icon..ctor), a causa di un
// disallineamento tra le versioni di System.Windows.Forms e
// System.Drawing.Common caricate dal processo NX. Main() prova prima a
// mitigare la causa (vedi TryPreloadCompatibleSystemDrawing) e ad aprire
// questa finestra; se anche cosi' dovesse fallire, il try/catch attorno
// alla sua creazione in Main() lo rileva e ripiega automaticamente su un
// flusso equivalente basato solo su MessageBox/FolderBrowserDialog
// (RunFallbackFlow, piu' sotto), che sono dialoghi nativi di Windows e non
// passano da Form.UpdateWindowIcon.

// Finestra unica (tre schermate, mai chiusa e riaperta nel mezzo) che funge
// da punto di ingresso del journal e da centro di controllo per l'intero
// flusso: 1) configurazione delle cartelle (con opzioni avanzate opzionali)
// e avvio della scansione; 2) SOLO SE la scansione trova davvero dei
// conflitti da risolvere, il riepilogo dei risultati e la scelta di come
// procedere (se non ci sono conflitti si passa direttamente al punto 3);
// 3) avanzamento della conversione in tempo reale (barra di progresso, file
// corrente, log live, bottone Annulla), che a fine conversione si trasforma
// nel riepilogo finale con il bottone per aprire la cartella di output. La
// finestra si chiude solo quando l'utente lo decide esplicitamente
// (Interrompi prima di iniziare, oppure Chiudi dal riepilogo finale).
public class LauncherForm : Form
{
    public string ResultInputFolder;
    public string ResultOutputFolder;
    public BatchDecision ChosenDecision = BatchDecision.Stop;

    // true non appena StartConversion() ha avviato RunBatch (a prescindere
    // dall'esito: completata, annullata o fallita): dice a Main() che la
    // conversione e' gia' stata gestita per intero dentro questa finestra,
    // quindi non deve rilanciarla dopo la chiusura del form.
    public bool ConversionRan = false;

    private readonly Session theSession;
    private readonly ListingWindow lw;

    private Panel panelConfig;
    private TextBox txtInputFolder;
    private TextBox txtOutputFolder;
    private Button btnBrowseInput;
    private Button btnBrowseOutput;
    private CheckBox chkShowAdvanced;
    private Panel panelAdvanced;
    private NumericUpDown numChordalTol;
    private NumericUpDown numAdjacencyTol;
    private NumericUpDown numAngularTol;
    private CheckBox chkExportNotClosed;
    private Button btnScan;

    private Panel panelResults;
    private Label lblSummary;
    private ListView lvResults;
    private Button btnBack;
    private Button btnStop;
    private Button btnOverwrite;
    private Button btnCopy;

    private Panel panelProgress;
    private Label lblProgressHeader;
    private Label lblCurrentFile;
    private ProgressBar progressBar;
    private TextBox txtLog;
    private Button btnCancel;
    private Button btnOpenOutput;
    private Button btnCloseSummary;
    private string lastResultOutputFolder;

    public LauncherForm(Session session, ListingWindow listingWindow, string initialInputFolder, string initialOutputFolder)
    {
        theSession = session;
        lw = listingWindow;

        // Ogni proprieta' "cosmetica" e' avvolta nel proprio try/catch
        // (metodi TrySetXxx sotto): se una di queste lancia un'eccezione in
        // questo ambiente, il costruttore prosegue comunque invece di
        // fallire subito - resta comunque il try/catch piu' esterno in
        // Main() a intercettare un fallimento piu' serio (es. in
        // ShowDialog) e passare al fallback.
        Text = "Conversione batch STEP -> STL";
        Width = 720;
        Height = 500;
        TrySetShowIcon(false);
        TrySetStartPosition(FormStartPosition.CenterScreen);
        TrySetMinimizeBox(false);
        TrySetMaximizeBox(false);
        TrySetFormBorderStyle(FormBorderStyle.FixedDialog);

        BuildConfigPanel(initialInputFolder, initialOutputFolder);
        BuildResultsPanel();
        BuildProgressPanel();

        Controls.Add(panelProgress);
        Controls.Add(panelResults);
        Controls.Add(panelConfig);

        ShowConfigScreen();
    }

    private void TrySetShowIcon(bool value)
    {
        try { ShowIcon = value; } catch (Exception) { }
    }

    private void TrySetStartPosition(FormStartPosition value)
    {
        try { StartPosition = value; } catch (Exception) { }
    }

    private void TrySetMinimizeBox(bool value)
    {
        try { MinimizeBox = value; } catch (Exception) { }
    }

    private void TrySetMaximizeBox(bool value)
    {
        try { MaximizeBox = value; } catch (Exception) { }
    }

    private void TrySetFormBorderStyle(FormBorderStyle value)
    {
        try { FormBorderStyle = value; } catch (Exception) { }
    }

    private void BuildConfigPanel(string initialInputFolder, string initialOutputFolder)
    {
        panelConfig = new Panel();
        panelConfig.Dock = DockStyle.Fill;

        Label lblTitle = new Label();
        lblTitle.Text = "=== Conversione batch STEP -> STL ===";
        lblTitle.SetBounds(20, 16, 660, 30);

        Label lblIn = new Label();
        lblIn.Text = "Cartella di input (file STEP):";
        lblIn.SetBounds(20, 70, 660, 20);

        txtInputFolder = new TextBox();
        txtInputFolder.Text = initialInputFolder;
        txtInputFolder.SetBounds(20, 92, 560, 24);

        btnBrowseInput = new Button();
        btnBrowseInput.Text = "Sfoglia...";
        btnBrowseInput.SetBounds(590, 91, 90, 26);
        btnBrowseInput.Click += BtnBrowseInput_Click;

        Label lblOut = new Label();
        lblOut.Text = "Cartella di output (file STL):";
        lblOut.SetBounds(20, 132, 660, 20);

        txtOutputFolder = new TextBox();
        txtOutputFolder.Text = initialOutputFolder;
        txtOutputFolder.SetBounds(20, 154, 560, 24);

        btnBrowseOutput = new Button();
        btnBrowseOutput.Text = "Sfoglia...";
        btnBrowseOutput.SetBounds(590, 153, 90, 26);
        btnBrowseOutput.Click += BtnBrowseOutput_Click;

        Label lblInfo = new Label();
        lblInfo.Text =
            "La scansione confronta i file STEP nella cartella di input con gli STL gia' presenti\n" +
            "nella cartella di output, senza aprire alcuna parte in NX e senza modificare nulla. Se\n" +
            "non trova conflitti la conversione parte subito; altrimenti ti verra' chiesto come procedere.";
        lblInfo.SetBounds(20, 196, 660, 54);

        chkShowAdvanced = new CheckBox();
        chkShowAdvanced.Text = "Mostra opzioni avanzate (tolleranze STL, superfici non chiuse)";
        chkShowAdvanced.SetBounds(20, 260, 400, 22);
        chkShowAdvanced.CheckedChanged += ChkShowAdvanced_CheckedChanged;

        BuildAdvancedPanel();

        btnScan = new Button();
        btnScan.Text = "Avvia scansione";
        btnScan.SetBounds(20, 400, 160, 32);
        btnScan.Click += BtnScan_Click;

        panelConfig.Controls.Add(lblTitle);
        panelConfig.Controls.Add(lblIn);
        panelConfig.Controls.Add(txtInputFolder);
        panelConfig.Controls.Add(btnBrowseInput);
        panelConfig.Controls.Add(lblOut);
        panelConfig.Controls.Add(txtOutputFolder);
        panelConfig.Controls.Add(btnBrowseOutput);
        panelConfig.Controls.Add(lblInfo);
        panelConfig.Controls.Add(chkShowAdvanced);
        panelConfig.Controls.Add(panelAdvanced);
        panelConfig.Controls.Add(btnScan);
    }

    // Sezione "Opzioni avanzate": nascosta di default (mostra/nasconde con
    // chkShowAdvanced), permette di modificare per questa esecuzione le
    // tolleranze STL e se esportare anche le superfici non chiuse, senza
    // dover editare il file .cs. I valori iniziali sono quelli configurati
    // di default in NXJournal.
    private void BuildAdvancedPanel()
    {
        panelAdvanced = new Panel();
        panelAdvanced.SetBounds(20, 286, 660, 106);
        panelAdvanced.Visible = false;

        Label lblChordal = new Label();
        lblChordal.Text = "Tolleranza cordale (chordal):";
        lblChordal.SetBounds(0, 2, 260, 20);

        numChordalTol = new NumericUpDown();
        numChordalTol.SetBounds(270, 0, 100, 22);
        numChordalTol.Minimum = 0.0001m;
        numChordalTol.Maximum = 10m;
        numChordalTol.DecimalPlaces = 4;
        numChordalTol.Increment = 0.0001m;
        numChordalTol.Value = (decimal)NXJournal.chordalTol;

        Label lblAdjacency = new Label();
        lblAdjacency.Text = "Tolleranza di adiacenza (adjacency):";
        lblAdjacency.SetBounds(0, 30, 260, 20);

        numAdjacencyTol = new NumericUpDown();
        numAdjacencyTol.SetBounds(270, 28, 100, 22);
        numAdjacencyTol.Minimum = 0.01m;
        numAdjacencyTol.Maximum = 50m;
        numAdjacencyTol.DecimalPlaces = 2;
        numAdjacencyTol.Increment = 0.01m;
        numAdjacencyTol.Value = (decimal)NXJournal.adjacencyTol;

        Label lblAngular = new Label();
        lblAngular.Text = "Tolleranza angolare (gradi):";
        lblAngular.SetBounds(0, 58, 260, 20);

        numAngularTol = new NumericUpDown();
        numAngularTol.SetBounds(270, 56, 100, 22);
        numAngularTol.Minimum = 0.1m;
        numAngularTol.Maximum = 90m;
        numAngularTol.DecimalPlaces = 1;
        numAngularTol.Increment = 0.5m;
        numAngularTol.Value = (decimal)NXJournal.angularTol;

        chkExportNotClosed = new CheckBox();
        chkExportNotClosed.Text = "Esporta anche le superfici non chiuse (mesh aperte)";
        chkExportNotClosed.SetBounds(400, 0, 260, 60);
        chkExportNotClosed.Checked = NXJournal.exportNotClosedMeshes;

        panelAdvanced.Controls.Add(lblChordal);
        panelAdvanced.Controls.Add(numChordalTol);
        panelAdvanced.Controls.Add(lblAdjacency);
        panelAdvanced.Controls.Add(numAdjacencyTol);
        panelAdvanced.Controls.Add(lblAngular);
        panelAdvanced.Controls.Add(numAngularTol);
        panelAdvanced.Controls.Add(chkExportNotClosed);
    }

    private void ChkShowAdvanced_CheckedChanged(object sender, EventArgs e)
    {
        panelAdvanced.Visible = chkShowAdvanced.Checked;
    }

    private void BuildResultsPanel()
    {
        panelResults = new Panel();
        panelResults.Dock = DockStyle.Fill;

        lblSummary = new Label();
        lblSummary.SetBounds(20, 16, 660, 70);

        lvResults = new ListView();
        lvResults.View = System.Windows.Forms.View.Details;
        lvResults.FullRowSelect = true;
        lvResults.SetBounds(20, 96, 660, 270);
        lvResults.Columns.Add("File STEP", 220);
        lvResults.Columns.Add("Stato", 110);
        lvResults.Columns.Add("Dettagli", 320);

        btnBack = new Button();
        btnBack.Text = "Torna indietro";
        btnBack.SetBounds(20, 400, 130, 32);
        btnBack.Click += BtnBack_Click;

        btnStop = new Button();
        btnStop.Text = "Interrompi";
        btnStop.SetBounds(300, 400, 110, 32);
        btnStop.Click += BtnStop_Click;

        btnOverwrite = new Button();
        btnOverwrite.Text = "Sovrascrivi";
        btnOverwrite.SetBounds(420, 400, 110, 32);
        btnOverwrite.Click += BtnOverwrite_Click;

        btnCopy = new Button();
        btnCopy.Text = "Copia in nuova cartella";
        btnCopy.SetBounds(540, 400, 140, 32);
        btnCopy.Click += BtnCopy_Click;

        panelResults.Controls.Add(lblSummary);
        panelResults.Controls.Add(lvResults);
        panelResults.Controls.Add(btnBack);
        panelResults.Controls.Add(btnStop);
        panelResults.Controls.Add(btnOverwrite);
        panelResults.Controls.Add(btnCopy);
    }

    private void BtnBrowseInput_Click(object sender, EventArgs e)
    {
        BrowseFolder(txtInputFolder);
    }

    private void BtnBrowseOutput_Click(object sender, EventArgs e)
    {
        BrowseFolder(txtOutputFolder);
    }

    private void BrowseFolder(TextBox target)
    {
        using (FolderBrowserDialog dlg = new FolderBrowserDialog())
        {
            if (Directory.Exists(target.Text))
            {
                dlg.SelectedPath = target.Text;
            }
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                target.Text = dlg.SelectedPath;
            }
        }
    }

    // Avvia la scansione. Se NON ci sono conflitti, non ha senso chiedere
    // Sovrascrivi/Copia (non c'e' nulla da sovrascrivere): la finestra si
    // chiude subito e Main() procede direttamente con la conversione. Solo
    // se ci sono conflitti reali si passa alla schermata dei risultati con
    // la scelta a 3 vie.
    private void BtnScan_Click(object sender, EventArgs e)
    {
        string inputFolderToScan = txtInputFolder.Text.Trim();
        string outputFolderToScan = txtOutputFolder.Text.Trim();

        if (!Directory.Exists(inputFolderToScan))
        {
            MessageBox.Show(this, "La cartella di input non esiste:\n" + inputFolderToScan,
                "Cartella non trovata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!Directory.Exists(outputFolderToScan))
        {
            try
            {
                Directory.CreateDirectory(outputFolderToScan);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Impossibile creare la cartella di output:\n" + ex.Message,
                    "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        ScanSummary summary;
        try
        {
            summary = NXJournal.PreScanConflicts(inputFolderToScan, outputFolderToScan);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Errore durante la scansione:\n" + ex.Message,
                "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        NXJournal.WriteScanSummaryFile(outputFolderToScan, summary);

        ResultInputFolder = inputFolderToScan;
        ResultOutputFolder = outputFolderToScan;

        if (summary.ConflictCount == 0)
        {
            StartConversion(BatchDecision.Overwrite);
            return;
        }

        PopulateResults(summary);
        ShowResultsScreen();
    }

    private void PopulateResults(ScanSummary summary)
    {
        lblSummary.Text = string.Format(
            "Trovati {0} conflitti su {1} file STEP (gli output corrispondenti esistono gia').\n" +
            "Nota: le sorgenti che generano piu' di un file totale (corpi solidi + superfici aperte)\n" +
            "verranno raggruppate in una sottocartella dedicata; le superfici aperte in una\n" +
            "sotto-sottocartella \"{2}\".",
            summary.ConflictCount, summary.TotalSteps, NXJournal.notClosedSubfolderName);

        lvResults.Items.Clear();
        foreach (ScanRow row in summary.Rows)
        {
            ListViewItem item = new ListViewItem(new string[] { row.StepBaseName, row.Status, row.Detail });
            lvResults.Items.Add(item);
        }
    }

    private void BtnBack_Click(object sender, EventArgs e)
    {
        ShowConfigScreen();
    }

    private void BtnStop_Click(object sender, EventArgs e)
    {
        ChosenDecision = BatchDecision.Stop;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BtnOverwrite_Click(object sender, EventArgs e)
    {
        StartConversion(BatchDecision.Overwrite);
    }

    private void BtnCopy_Click(object sender, EventArgs e)
    {
        StartConversion(BatchDecision.CopyToNewFolder);
    }

    // Avvia la conversione vera e propria SENZA chiudere la finestra: si
    // passa al pannello di avanzamento (stessa finestra) e si chiama
    // direttamente RunBatch, che nel frattempo aggiorna log/barra tramite i
    // metodi AppendLogLine/SetProgress sotto. La finestra si chiude solo
    // quando l'utente preme "Chiudi" nel riepilogo finale.
    private void StartConversion(BatchDecision decision)
    {
        ChosenDecision = decision;
        ResultInputFolder = txtInputFolder.Text.Trim();
        ResultOutputFolder = txtOutputFolder.Text.Trim();

        NXJournal.inputFolder = ResultInputFolder;
        NXJournal.configuredOutputFolder = ResultOutputFolder;
        NXJournal.ApplyAdvancedOptions(
            (double)numChordalTol.Value, (double)numAdjacencyTol.Value,
            (double)numAngularTol.Value, chkExportNotClosed.Checked);

        txtLog.Clear();
        progressBar.Value = 0;
        lblProgressHeader.Text = "Conversione in corso...";
        lblCurrentFile.Text = "Preparazione in corso...";
        btnCancel.Enabled = true;
        btnCancel.Text = "Annulla";
        btnCancel.Visible = true;
        btnOpenOutput.Visible = false;
        btnCloseSummary.Visible = false;
        ShowProgressScreen();
        Application.DoEvents();

        NXJournal.ActiveProgressForm = this;
        NXJournal.CancelRequested = false;
        ConversionRan = true;

        NXJournal.BatchResult result;
        try
        {
            result = NXJournal.RunBatch(theSession, lw, decision);
        }
        catch (Exception ex)
        {
            NXJournal.Log(lw, "ERRORE GENERALE (la conversione si e' fermata): " + ex.Message);
            NXJournal.Log(lw, ex.StackTrace);
            result = new NXJournal.BatchResult();
            result.OutputFolder = NXJournal.outputFolder;
            result.FatalError = ex.Message;
        }
        finally
        {
            NXJournal.ActiveProgressForm = null;
        }

        ShowSummary(result);
    }

    private void BtnCancel_Click(object sender, EventArgs e)
    {
        NXJournal.CancelRequested = true;
        btnCancel.Enabled = false;
        btnCancel.Text = "Annullamento in corso...";
    }

    private void BtnOpenOutput_Click(object sender, EventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(lastResultOutputFolder) && Directory.Exists(lastResultOutputFolder))
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + lastResultOutputFolder + "\"");
            }
        }
        catch (Exception)
        {
            // apertura della cartella puramente di comodo: se fallisce non blocchiamo nulla
        }
    }

    private void BtnCloseSummary_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    // Chiamato da NXJournal.Log() per ogni riga, se questa finestra e' il
    // form di avanzamento attivo: tiene il log della GUI allineato in tempo
    // reale a quello che va nella Listing Window/file di log.
    internal void AppendLogLine(string message)
    {
        if (txtLog == null || txtLog.IsDisposed)
        {
            return;
        }
        txtLog.AppendText(message + Environment.NewLine);
    }

    // Chiamato da NXJournal.ReportProgress() a inizio di ogni file STEP.
    internal void SetProgress(int current, int total, string label)
    {
        if (progressBar == null || progressBar.IsDisposed)
        {
            return;
        }
        if (total > 0)
        {
            progressBar.Maximum = total;
            progressBar.Value = Math.Min(Math.Max(current, 0), total);
        }
        lblCurrentFile.Text = label;
    }

    private void ShowSummary(NXJournal.BatchResult result)
    {
        lastResultOutputFolder = result.OutputFolder;

        string headline;
        if (result.FatalError != null)
        {
            headline = "Conversione interrotta da un errore imprevisto: " + result.FatalError;
        }
        else if (result.Cancelled)
        {
            headline = string.Format(
                "Annullato dall'utente: {0} file STEP completati su {1}, {2} file STL scritti.",
                result.Ok + result.Failed, result.TotalSteps, result.GrandFiles);
        }
        else
        {
            headline = string.Format(
                "Completato: {0} file STEP riusciti su {1}, {2} file STL scritti.",
                result.Ok, result.TotalSteps, result.GrandFiles);
            if (result.Failed > 0)
            {
                headline += string.Format(" ({0} file STEP falliti: vedi log errori.)", result.Failed);
            }
        }

        lblProgressHeader.Text = headline;
        lblCurrentFile.Text = "Cartella di output: " + result.OutputFolder;

        btnCancel.Visible = false;
        btnOpenOutput.Visible = true;
        btnCloseSummary.Visible = true;
    }

    private void ShowConfigScreen()
    {
        panelResults.Visible = false;
        panelProgress.Visible = false;
        panelConfig.Visible = true;
    }

    private void ShowResultsScreen()
    {
        panelConfig.Visible = false;
        panelProgress.Visible = false;
        panelResults.Visible = true;
    }

    private void ShowProgressScreen()
    {
        panelConfig.Visible = false;
        panelResults.Visible = false;
        panelProgress.Visible = true;
    }

    // Costruisce il terzo pannello (avanzamento -> diventa riepilogo a fine
    // conversione, vedi ShowSummary): barra di progresso, log testuale live
    // (rispecchia la Listing Window/il file di log) e i pulsanti Annulla /
    // Apri cartella di output / Chiudi (questi ultimi due nascosti finche'
    // la conversione non e' finita).
    private void BuildProgressPanel()
    {
        panelProgress = new Panel();
        panelProgress.Dock = DockStyle.Fill;

        lblProgressHeader = new Label();
        lblProgressHeader.Text = "Conversione in corso...";
        lblProgressHeader.SetBounds(20, 16, 660, 38);

        lblCurrentFile = new Label();
        lblCurrentFile.Text = "";
        lblCurrentFile.SetBounds(20, 58, 660, 20);

        progressBar = new ProgressBar();
        progressBar.SetBounds(20, 82, 660, 22);
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;

        txtLog = new TextBox();
        txtLog.SetBounds(20, 112, 660, 254);
        txtLog.Multiline = true;
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.WordWrap = false;

        btnCancel = new Button();
        btnCancel.Text = "Annulla";
        btnCancel.SetBounds(20, 400, 110, 32);
        btnCancel.Click += BtnCancel_Click;

        btnOpenOutput = new Button();
        btnOpenOutput.Text = "Apri cartella di output";
        btnOpenOutput.SetBounds(430, 400, 150, 32);
        btnOpenOutput.Click += BtnOpenOutput_Click;
        btnOpenOutput.Visible = false;

        btnCloseSummary = new Button();
        btnCloseSummary.Text = "Chiudi";
        btnCloseSummary.SetBounds(590, 400, 90, 32);
        btnCloseSummary.Click += BtnCloseSummary_Click;
        btnCloseSummary.Visible = false;

        panelProgress.Controls.Add(lblProgressHeader);
        panelProgress.Controls.Add(lblCurrentFile);
        panelProgress.Controls.Add(progressBar);
        panelProgress.Controls.Add(txtLog);
        panelProgress.Controls.Add(btnCancel);
        panelProgress.Controls.Add(btnOpenOutput);
        panelProgress.Controls.Add(btnCloseSummary);
    }

    // Chiusura della finestra (es. [X], Alt+F4) senza aver premuto nessuno dei
    // pulsanti di decisione equivale sempre a "Interrompi" (default sicuro),
    // ma solo se la conversione non e' gia' partita (altrimenti manterrebbe
    // comunque il risultato gia' prodotto: chiudere a conversione finita non
    // deve "annullare" nulla).
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK && !ConversionRan)
        {
            ChosenDecision = BatchDecision.Stop;
        }
        base.OnFormClosing(e);
    }
}

public class NXJournal
{
    // =========================================================================
    // CONFIGURAZIONE (valori di default: modificabili anche dalla GUI ad ogni
    // esecuzione, senza dover editare questo file)
    // =========================================================================
    // internal (non piu' private): LauncherForm imposta questi campi con i
    // valori scelti dall'utente subito prima di avviare RunBatch (vedi
    // StartConversion), esattamente come faceva gia' Main() con il vecchio
    // flusso a finestra "usa e getta".
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
    // avanzate" di LauncherForm (vedi ApplyAdvancedOptions) subito prima di
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
    // dentro la sottocartella dedicata alla sorgente. Accessibile anche da
    // LauncherForm per il testo di riepilogo della scansione.
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

    // Form di avanzamento attivo (se presente): quando non null, Log() vi
    // rispecchia ogni riga in tempo reale e ReportProgress() ne aggiorna la
    // barra di avanzamento. Impostato/azzerato da LauncherForm.StartConversion.
    internal static LauncherForm ActiveProgressForm = null;

    // Flag cooperativo impostato dal bottone "Annulla" del pannello di
    // avanzamento: controllato da RunBatch a inizio di ogni file STEP (mai a
    // meta' esportazione), cosi' l'interruzione avviene sempre a un confine
    // sicuro tra un file e il successivo.
    internal static volatile bool CancelRequested = false;

    // Risultato di un'esecuzione di RunBatch: i conteggi erano gia' tutti
    // calcolati a fine metodo (variabili locali), qui vengono solo raccolti
    // in un oggetto cosi' che LauncherForm possa mostrarli nel riepilogo
    // finale invece che solo nella Listing Window.
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

    // Chiamato da RunBatch a inizio di ogni file STEP: aggiorna la barra di
    // avanzamento del form attivo, se presente. Avvolto in try/catch come
    // tutte le altre interazioni con la GUI in questo file.
    internal static void ReportProgress(int current, int total, string label)
    {
        if (ActiveProgressForm == null)
        {
            return;
        }
        try
        {
            ActiveProgressForm.SetProgress(current, total, label);
        }
        catch (Exception)
        {
            // un aggiornamento di progresso mancato non deve bloccare la conversione
        }
    }

    public static void Main(string[] args)
    {
        Session theSession = Session.GetSession();
        ListingWindow lw = theSession.ListingWindow;
        lw.Open();

        Log(lw, "=== Avvio conversione batch STEP -> STL (v11) ===");

        TryPreloadCompatibleSystemDrawing();

        // Percorso preferito: finestra grafica vera (LauncherForm), che ora
        // gestisce l'INTERO flusso al suo interno (configurazione, eventuale
        // risoluzione conflitti, avanzamento live e riepilogo finale) senza
        // mai chiudersi e riaprirsi: la conversione vera e propria viene
        // avviata direttamente da dentro la finestra (vedi
        // LauncherForm.StartConversion), non piu' da qui dopo la sua
        // chiusura. Se in questo ambiente NX qualunque Form crasha alla
        // creazione (vedi commento sopra LauncherForm), il try/catch lo
        // rileva e si passa automaticamente al fallback a soli
        // MessageBox/FolderBrowserDialog (RunFallbackFlow) - in quel caso
        // pero' non c'e' un pannello di avanzamento dedicato: e' il percorso
        // di compatibilita' degradata, non l'esperienza primaria.
        BatchDecision decision = BatchDecision.Stop;
        bool formUiSucceeded = false;
        bool conversionHandledByForm = false;
        try
        {
            using (LauncherForm launcher = new LauncherForm(theSession, lw, inputFolder, configuredOutputFolder))
            {
                launcher.ShowDialog();
                decision = launcher.ChosenDecision;
                conversionHandledByForm = launcher.ConversionRan;
                if (!string.IsNullOrEmpty(launcher.ResultInputFolder))
                {
                    inputFolder = launcher.ResultInputFolder;
                }
                if (!string.IsNullOrEmpty(launcher.ResultOutputFolder))
                {
                    configuredOutputFolder = launcher.ResultOutputFolder;
                }
                formUiSucceeded = true;
            }
        }
        catch (Exception ex)
        {
            decision = BatchDecision.Stop;
            Log(lw, "Interfaccia grafica avanzata non disponibile in questo ambiente NX (" +
                ex.GetType().Name + ": " + ex.Message + "). Passo ai popup di sistema.");
        }

        if (!formUiSucceeded)
        {
            decision = RunFallbackFlow(lw);
        }

        if (conversionHandledByForm)
        {
            // La conversione e' gia' stata eseguita per intero dentro la
            // finestra (compresi log incrementale e riepilogo): qui resta
            // solo da scrivere la rete di sicurezza del log completo.
            WriteFinalLogSafety();
            return;
        }

        outputFolder = configuredOutputFolder;

        if (decision == BatchDecision.Stop)
        {
            LogStopAndExit(lw, "Interrotto dall'utente prima di avviare la conversione. Nessun file scritto.");
            return;
        }

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

    // Tentativo best-effort di far usare al processo la versione di
    // System.Drawing.Common che sta nella STESSA cartella di
    // System.Windows.Forms.dll (le due vengono sempre distribuite insieme
    // nello stesso "shared framework" .NET). Se NX ha gia' caricato una
    // copia diversa/incompatibile PRIMA che questo journal partisse (causa
    // nota del MissingMethodException su Form.UpdateWindowIcon), questo
    // puo' non avere alcun effetto perche' l'identita' dell'assembly e' gia'
    // stata risolta nel processo; ma se invece nessuno l'ha ancora caricata,
    // forzare qui la versione "giusta" puo' evitare il problema alla radice.
    // Avvolto in try/catch: se fallisce, non peggiora nulla rispetto a
    // prima, e il try/catch attorno a LauncherForm in Main() gestisce
    // comunque un eventuale fallimento residuo.
    private static void TryPreloadCompatibleSystemDrawing()
    {
        try
        {
            string wfDir = Path.GetDirectoryName(typeof(Form).Assembly.Location);
            if (string.IsNullOrEmpty(wfDir))
            {
                return;
            }
            string drawingPath = Path.Combine(wfDir, "System.Drawing.Common.dll");
            if (File.Exists(drawingPath))
            {
                System.Reflection.Assembly.LoadFrom(drawingPath);
            }
        }
        catch (Exception)
        {
            // best-effort: se non funziona, il fallback in Main() gestisce comunque il crash
        }
    }

    private static void LogStopAndExit(ListingWindow lw, string message)
    {
        Log(lw, message);
        WriteFinalLogSafety();
    }

    // Percorso di riserva, usato SOLO se LauncherForm non e' utilizzabile in
    // questo ambiente NX. Stessa logica "chiedi solo se serve" della
    // finestra grafica: un solo selettore di cartella per input e uno per
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

        // Meccanismo di annullamento cooperativo che funziona anche quando la
        // Form vera non e' disponibile (fallback a soli MessageBox): oltre al
        // flag CancelRequested (usato dal bottone "Annulla" della Form), si
        // controlla anche l'esistenza di un file marker. Chi sta usando il
        // fallback puo' annullare creando manualmente questo file (es. da
        // Esplora risorse) durante l'esecuzione.
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
            if (CancelRequested || File.Exists(cancelMarkerPath))
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
            ReportProgress(i + 1, stepFiles.Count, string.Format("File {0}/{1}: {2}", i + 1, stepFiles.Count, baseName));
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

        ReportProgress(stepFiles.Count, stepFiles.Count, cancelled ? "Annullato dall'utente." : "Conversione completata.");

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

    // internal (non piu' private): LauncherForm.StartConversion la chiama
    // direttamente in caso di eccezione fatale durante RunBatch.
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

        // Rispecchia la riga anche nel pannello di avanzamento della GUI, se
        // presente, e lascia respirare la finestra (ridisegno, click su
        // "Annulla") durante il loop sincrono di RunBatch - NXOpen non e'
        // thread-safe, quindi non si puo' usare un thread separato per la
        // conversione: Application.DoEvents() e' il modo con cui il resto di
        // questo file gia' mantiene la GUI reattiva senza multithreading.
        if (ActiveProgressForm != null)
        {
            try
            {
                ActiveProgressForm.AppendLogLine(message);
                Application.DoEvents();
            }
            catch (Exception)
            {
                // un aggiornamento di log mancato non deve bloccare la conversione
            }
        }
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
