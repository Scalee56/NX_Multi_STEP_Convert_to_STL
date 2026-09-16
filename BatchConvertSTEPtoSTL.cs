// =============================================================================
// NX Open Journal - Conversione massiva STEP -> STL (v15)
// Basato sul journal originale "journal.cs" (export singolo STL registrato in NX),
// esteso per scorrere automaticamente tutti i file .stp/.step di una cartella.
//
// COSA E' STATO CORRETTO/AGGIUNTO IN QUESTA VERSIONE (v15):
// - FIX CRITICO: l'apertura di un file STEP (o di un .prt di componente gia'
//   noto) con OpenActiveDisplay(..., DisplayPartOption.AllowAdditional, ...)
//   non garantisce che la parte aperta diventi anche la "Work part" (puo'
//   restare solo "Display part", tipicamente per il primo file aperto nella
//   sessione) - ma ApplicationSwitchImmediate("UG_APP_MODELING") richiede
//   una Work part valida, quindi falliva SEMPRE con "Cannot enter the
//   specified application since a part is required", portando a 0
//   conversioni riuscite. Ora la Work part viene impostata esplicitamente
//   (Parts.SetWork) se OpenActiveDisplay non l'ha gia' fatto, con un errore
//   chiaro invece di quello generico se anche questo non basta.
// - AGGIUNTA anteprima del pezzo in elaborazione (JPG esportato direttamente
//   da NX via ImageExportBuilder, nessuna dipendenza da System.Drawing.Common
//   lato NX), tempo trascorso e rotella di caricamento animata nel pannello
//   di avanzamento - la preview restava sempre vuota quando la conversione
//   falliva per il motivo sopra (CaptureAndDisplayScreenshot richiede
//   Parts.Display non null, mai il caso quando l'apertura falliva).
// - RIFATTA l'architettura della GUI esterna (PowerShell): prima ogni fase
//   (Config / Decisione / Avanzamento / Riepilogo) apriva un processo
//   powershell.exe/finestra separati, dando l'impressione che le finestre si
//   chiudessero e riaprissero continuamente. Ora c'e' UN SOLO processo,
//   avviato una volta sola, la cui finestra resta aperta per tutto il run e
//   cambia semplicemente pannello al proprio interno; mentre il journal C#
//   calcola la fase successiva (scansione conflitti, avvio conversione,
//   riepilogo finale) la stessa finestra mostra un pannello di attesa con
//   una rotella di caricamento, invece di sparire e ricomparire.
//
// COSA ERA STATO CORRETTO/AGGIUNTO IN v14.2:
// - FIX: il primo campo di testo/il testo di riepilogo appariva sempre
//   completamente selezionato (evidenziato in blu) all'apertura di ogni
//   finestra - comportamento di default di WinForms quando un controllo
//   riceve il focus iniziale. Ora il cursore in Config parte a inizio testo
//   senza selezione, e le caselle di riepilogo (sola lettura) non possono
//   piu' ricevere il focus.
// - FIX: le etichette (e le caselle di spunta) avevano uno sfondo grigio
//   opaco invece che trasparente - visibile come un rettangolo grigio
//   sopra le card bianche. Ora tutte le etichette hanno sfondo trasparente.
// - FIX: i pulsanti bianchi (Sfoglia, Annulla, ecc.) mostravano un bordino
//   scuro e angoli poco arrotondati - causato dallo scaling automatico di
//   WinForms in base al DPI dello schermo, applicato DOPO che le regioni
//   arrotondate erano gia' state calcolate. Disattivato lo scaling
//   automatico e sostituito bianco+bordo con un riempimento grigio chiaro
//   pieno, piu' robusto verso questo tipo di disallineamento.
//
// COSA ERA STATO CORRETTO/AGGIUNTO IN v14:
// - ULTERIORE RIFINITURA GRAFICA della GUI esterna: gruppi di campi racchiusi
//   in "card" bianche con angoli arrotondati su sfondo grigio chiaro (stile
//   pannelli di Impostazioni di macOS), dissolvenza in apertura per tutte le
//   finestre, e barra di avanzamento che ora scorre con un'animazione fluida
//   verso la percentuale corrente invece di scattare di colpo.
// - OTTIMIZZAZIONE VELOCITA': il file di log incrementale (log_conversione.txt)
//   veniva riaperto e richiuso ad ogni singola riga scritta durante il batch
//   (File.AppendAllText). Ora l'handle resta aperto per tutta la conversione
//   (con Flush() dopo ogni riga per mantenere la stessa protezione in caso di
//   crash): elimina un overhead di apertura/chiusura file per ogni riga di
//   log, piu' evidente su cartelle di rete o con antivirus che intercetta
//   ogni apertura file.
//
// COSA ERA STATO CORRETTO/AGGIUNTO IN v13:
// - RESTYLING GRAFICO di tutte le finestre della GUI esterna (PowerShell):
//   font Segoe UI, palette chiara, pulsanti piatti con angoli arrotondati e
//   colore d'accento al passaggio del mouse, al posto dei controlli WinForms
//   di default (bordi 3D "vecchio Windows").
// - TORNATA la finestra di avanzamento in tempo reale con barra di progresso
//   (era stata rimossa in v12 insieme a LauncherForm): e' di nuovo un
//   processo powershell.exe separato, avviato senza bloccare la conversione,
//   che mostra file corrente, percentuale, conteggio riusciti/falliti e un
//   pulsante Annulla (scrive lo stesso marker CANCEL.txt gia' in uso). Si
//   aggiorna leggendo un file di stato scritto ad ogni file STEP processato,
//   e si chiude da sola a fine conversione.
//
// COSA ERA STATO CORRETTO/AGGIUNTO IN v12:
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

    // La generazione della preview forza NX a renderizzare e scrivere un JPG.
    // Su batch/server e assiemi con molte occorrenze il costo e' sensibile;
    // resta quindi opt-in. La barra di avanzamento continua a funzionare anche
    // senza immagine.
    internal static bool enableProgressPreview = false;

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

    // StreamWriter tenuto aperto per tutta la durata del batch, cosi' che
    // Log() non debba piu' aprire e richiudere il file ad ogni singola riga
    // (File.AppendAllText apriva/chiudeva l'handle ad ogni chiamata: su
    // centinaia di file, con diverse righe di log ciascuno, il solo overhead
    // di apertura/chiusura - specialmente su cartelle di rete o con antivirus
    // che intercetta ogni apertura file - poteva diventare un rallentamento
    // misurabile). Il buffer viene scaricato al termine di ogni STEP: il log
    // resta recuperabile durante il batch senza pagare un flush per riga.
    private static StreamWriter logWriter = null;
    private static bool incrementalLogHealthy = false;

    // Cartella temporanea creata per lo scambio di file con il processo
    // powershell.exe della GUI esterna (vedi RunExternalGuiFlow), e percorso
    // dello script .ps1 scritto al suo interno. Restano valorizzati per
    // tutta la durata del run cosi' da poter mostrare anche la finestra di
    // riepilogo finale (TryShowExternalGuiSummary) con lo stesso script gia'
    // scritto, e per poter ripulire tutto a fine esecuzione
    // (CleanUpExternalGuiWorkDir). Null se la GUI esterna non e' mai partita.
    private static string externalGuiWorkDir = null;

    // Processo powershell.exe UNICO che ospita la GUI esterna per l'intera
    // durata del run (config -> eventuale decisione -> avanzamento ->
    // riepilogo): a differenza delle versioni precedenti (un processo per
    // stage), qui la finestra resta la stessa per tutto il flusso e cambia
    // solo pannello internamente, cosi' non si vede mai chiudere/riaprire.
    // Null se la GUI esterna non e' mai partita.
    private static Process externalGuiProcess = null;
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

        Log(lw, "=== Avvio conversione batch STEP -> STL (v15) ===");

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
            TryCloseExternalGuiProcess();
            CleanUpExternalGuiWorkDir();
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
        else if (externalGuiSucceeded)
        {
            // RunBatch e' uscito con un errore generale (result e' rimasto
            // null): nessun riepilogo da mostrare, ma la finestra persistente
            // e' comunque ancora aperta (probabilmente ferma sul pannello di
            // attesa) e nessuno le dira' piu' cosa fare dopo - va chiusa
            // esplicitamente, altrimenti resterebbe li' per sempre.
            TryCloseExternalGuiProcess();
        }

        CleanUpExternalGuiWorkDir();
    }

    // Script PowerShell della GUI esterna: un unico file, scritto su disco
    // una volta per run e lanciato UNA SOLA volta come processo persistente
    // (vedi StartPersistentExternalGui), che resta aperto per l'intera
    // durata del flusso (Config / eventuale Decision / Progress / Summary) e
    // cambia semplicemente pannello al suo interno - mai un nuovo processo o
    // una nuova finestra per fase (vedi il commento "Finestra unica
    // persistente" dentro lo script). Ogni fase legge il proprio file di
    // input e scrive il proprio file di output dentro -WorkDir, in un
    // formato "chiave=valore" volutamente elementare (niente libreria JSON
    // necessaria su nessuno dei due lati); il file "next_stage.txt" e' il
    // segnale con cui il lato C# dice alla finestra (ferma sul pannello di
    // attesa) quale pannello mostrare quando ha finito di calcolare la fase
    // successiva. Tutti i numeri sono sempre formattati/parsati con cultura
    // invariante (punto come separatore decimale), per non dipendere dalle
    // impostazioni regionali della macchina (es. virgola invece di punto con
    // Windows in italiano).
    private static readonly string ExternalGuiScriptSource =
@"param(
    [Parameter(Mandatory=$true)][string]$WorkDir
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ic = [System.Globalization.CultureInfo]::InvariantCulture

# --- Palette e stile condivisi da tutte le finestre (look chiaro e piatto, stile ""Apple"") ---
$ClrWindowBg   = [System.Drawing.Color]::FromArgb(246, 246, 248)
$ClrCardBg     = [System.Drawing.Color]::White
$ClrText       = [System.Drawing.Color]::FromArgb(29, 29, 31)
$ClrSubtext    = [System.Drawing.Color]::FromArgb(110, 110, 115)
$ClrBorder     = [System.Drawing.Color]::FromArgb(216, 216, 220)
$ClrAccent     = [System.Drawing.Color]::FromArgb(0, 122, 255)
$ClrAccentDark = [System.Drawing.Color]::FromArgb(0, 100, 220)
$ClrTrack      = [System.Drawing.Color]::FromArgb(228, 228, 232)

$FontBase   = New-Object System.Drawing.Font(""Segoe UI"", 9.5)
$FontTitle  = New-Object System.Drawing.Font(""Segoe UI"", 13, [System.Drawing.FontStyle]::Bold)
$FontSmall  = New-Object System.Drawing.Font(""Segoe UI"", 8.75)
$FontButton = New-Object System.Drawing.Font(""Segoe UI"", 9.5, [System.Drawing.FontStyle]::Bold)

# Regione con angoli arrotondati, usata per dare ai pulsanti, alle card e alla
# barra di avanzamento un aspetto piu' morbido del rettangolo vivo di default
# di WinForms.
function New-RoundedRegion($w, $h, $r) {
    $d = $r * 2
    if ($d -gt $w) { $d = $w }
    if ($d -gt $h) { $d = $h }
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
    $path.AddArc(0, $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return New-Object System.Drawing.Region($path)
}

function Style-Form($form) {
    # AutoScaleMode di default (Font) fa si' che WinForms ridimensioni tutti
    # i controlli DOPO che erano gia' stati posizionati/arrotondati con le
    # coordinate assolute usate qui, su schermi con scaling DPI diverso dal
    # 100%: il risultato e' un disallineamento fra i bordi dei pulsanti e la
    # loro regione arrotondata (angoli poco arrotondati, bordino residuo).
    # Disattivandolo i controlli restano esattamente alle coordinate/misure
    # con cui sono stati creati.
    $form.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::None
    $form.BackColor = $ClrWindowBg
    $form.Font = $FontBase
}

# Pannello decorativo bianco con angoli arrotondati, usato come sfondo ""a
# scheda"" dietro a un gruppo di controlli gia' posizionati sul form (i
# controlli restano figli diretti del form, alle loro coordinate originali:
# la card viene solo disegnata dietro di essi, senza bisogno di ricalcolare
# le coordinate di nulla). IMPORTANTE: SendToBack() va richiamato dal
# chiamante DOPO aver aggiunto tutti i controlli che devono comparire sopra
# la card - chiamarlo qui, subito dopo la creazione (quando la card e'
# ancora l'unico controllo nella collezione), non ha alcun effetto duraturo:
# i controlli aggiunti in seguito finiscono comunque sopra di essa in modo
# imprevedibile.
function Add-Card($form, $x, $y, $w, $h) {
    $card = New-Object System.Windows.Forms.Panel
    $card.SetBounds($x, $y, $w, $h)
    $card.BackColor = $ClrCardBg
    $card.Region = New-RoundedRegion $card.Width $card.Height 12
    $form.Controls.Add($card)
    return $card
}

function Style-TitleLabel($lbl) {
    $lbl.Font = $FontTitle
    $lbl.ForeColor = $ClrText
    $lbl.BackColor = [System.Drawing.Color]::Transparent
}

function Style-Label($lbl) {
    $lbl.Font = $FontBase
    $lbl.ForeColor = $ClrText
    $lbl.BackColor = [System.Drawing.Color]::Transparent
}

function Style-SubLabel($lbl) {
    $lbl.Font = $FontSmall
    $lbl.ForeColor = $ClrSubtext
    $lbl.BackColor = [System.Drawing.Color]::Transparent
}

function Style-TextBox($txt) {
    $txt.Font = $FontBase
    $txt.BorderStyle = ""FixedSingle""
    $txt.BackColor = $ClrCardBg
    $txt.ForeColor = $ClrText
}

function Style-NumericUpDown($num) {
    $num.Font = $FontBase
    $num.BorderStyle = ""FixedSingle""
    $num.BackColor = $ClrCardBg
    $num.ForeColor = $ClrText
}

function Style-CheckBox($chk) {
    $chk.Font = $FontBase
    $chk.ForeColor = $ClrText
    $chk.BackColor = [System.Drawing.Color]::Transparent
}

function Style-PrimaryButton($btn) {
    $btn.FlatStyle = ""Flat""
    $btn.FlatAppearance.BorderSize = 0
    $btn.BackColor = $ClrAccent
    $btn.ForeColor = [System.Drawing.Color]::White
    $btn.Font = $FontButton
    $btn.Cursor = [System.Windows.Forms.Cursors]::Hand
    $btn.Region = New-RoundedRegion $btn.Width $btn.Height 8
    $btn.Add_MouseEnter({ $this.BackColor = $ClrAccentDark })
    $btn.Add_MouseLeave({ $this.BackColor = $ClrAccent })
}

function Style-SecondaryButton($btn) {
    # Niente FlatAppearance.BorderSize: un bordo rettangolare disegnato sopra
    # una regione arrotondata lascia un piccolo residuo agli angoli (il
    # ""bordino"" visibile). Un riempimento pieno grigio chiaro invece del
    # bianco+bordo evita del tutto il problema e resta leggibile come
    # pulsante anche sopra le card bianche.
    $btn.FlatStyle = ""Flat""
    $btn.FlatAppearance.BorderSize = 0
    $btn.BackColor = $ClrTrack
    $btn.ForeColor = $ClrText
    $btn.Font = $FontButton
    $btn.Cursor = [System.Windows.Forms.Cursors]::Hand
    $btn.Region = New-RoundedRegion $btn.Width $btn.Height 8
    $btn.Add_MouseEnter({ $this.BackColor = $ClrBorder })
    $btn.Add_MouseLeave({ $this.BackColor = $ClrTrack })
}

# Dissolvenza in apertura: il form parte invisibile (Opacity 0) e, non
# appena viene mostrato, un timer lo porta a piena opacita' in pochi
# passaggi. Puramente cosmetico: si ferma e si smonta da solo, non ha alcun
# effetto sul contenuto o sul risultato del dialogo.
function Enable-FadeIn($form) {
    $form.Opacity = 0
    $form.Add_Shown({
        $fadeTimer = New-Object System.Windows.Forms.Timer
        $fadeTimer.Interval = 15
        $fadeTimer.Add_Tick({
            $next = $form.Opacity + 0.15
            if ($next -ge 1.0) {
                $form.Opacity = 1.0
                $fadeTimer.Stop()
                $fadeTimer.Dispose()
            } else {
                $form.Opacity = $next
            }
        })
        $fadeTimer.Start()
    })
}

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

# --- Finestra unica persistente -------------------------------------------
# A differenza delle versioni precedenti (un processo powershell.exe per
# stage, ognuno con il proprio Form/ShowDialog), qui viene creato UN SOLO
# Form, mostrato UNA SOLA volta con ShowDialog() alla fine dello script: ogni
# ""stage"" (Config / Attesa / Decisione / Avanzamento / Riepilogo) e' un
# Panel separato, gia' costruito in anticipo, che viene semplicemente reso
# visibile (nascondendo gli altri) al momento giusto. Cosi' la finestra non
# si chiude e riapre mai fra una fase e l'altra: quando il journal C# (dentro
# NX) deve calcolare qualcosa (la scansione conflitti, l'avvio della
# conversione, il riepilogo finale) la stessa finestra mostra semplicemente
# il pannello ""Attesa"" con una rotella di caricamento, finche' il file di
# comando ""next_stage.txt"" non le dice quale pannello mostrare dopo.
function Center-Form($targetForm, $w, $h) {
    $targetForm.Width = $w
    $targetForm.Height = $h
    $wa = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $x = $wa.X + [int](($wa.Width - $targetForm.Width) / 2)
    $y = $wa.Y + [int](($wa.Height - $targetForm.Height) / 2)
    $targetForm.Location = New-Object System.Drawing.Point($x, $y)
}

$form = New-Object System.Windows.Forms.Form
$form.StartPosition = ""Manual""
$form.FormBorderStyle = ""FixedDialog""
$form.MinimizeBox = $false
$form.MaximizeBox = $false
$form.Topmost = $true
Style-Form $form

$configInputFile = Join-Path $WorkDir ""config_input.txt""
$cfg = Read-KeyValueFile $configInputFile

# --- Pannello Config --------------------------------------------------------
$pnlConfig = New-Object System.Windows.Forms.Panel
$pnlConfig.SetBounds(0, 0, 720, 580)
$pnlConfig.BackColor = $ClrWindowBg
$form.Controls.Add($pnlConfig)

$lblTitleConfig = New-Object System.Windows.Forms.Label
$lblTitleConfig.Text = ""Conversione batch STEP -> STL""
$lblTitleConfig.SetBounds(24, 20, 650, 30)
Style-TitleLabel $lblTitleConfig
$pnlConfig.Controls.Add($lblTitleConfig)

$cardCartelle = Add-Card $pnlConfig 14 66 620 178

$lblIn = New-Object System.Windows.Forms.Label
$lblIn.Text = ""Cartella di input (file STEP):""
$lblIn.SetBounds(24, 74, 580, 20)
Style-Label $lblIn
$pnlConfig.Controls.Add($lblIn)

$txtIn = New-Object System.Windows.Forms.TextBox
$txtIn.SetBounds(24, 98, 500, 26)
$txtIn.Text = $cfg[""InputFolder""]
Style-TextBox $txtIn
$pnlConfig.Controls.Add($txtIn)

$btnBrowseIn = New-Object System.Windows.Forms.Button
$btnBrowseIn.Text = ""Sfoglia""
$btnBrowseIn.SetBounds(536, 96, 88, 30)
$pnlConfig.Controls.Add($btnBrowseIn)
Style-SecondaryButton $btnBrowseIn
$btnBrowseIn.Add_Click({
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    if (Test-Path -LiteralPath $txtIn.Text) { $dlg.SelectedPath = $txtIn.Text }
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $txtIn.Text = $dlg.SelectedPath }
})

$lblOut = New-Object System.Windows.Forms.Label
$lblOut.Text = ""Cartella di output (file STL):""
$lblOut.SetBounds(24, 138, 580, 20)
Style-Label $lblOut
$pnlConfig.Controls.Add($lblOut)

$txtOut = New-Object System.Windows.Forms.TextBox
$txtOut.SetBounds(24, 162, 500, 26)
$txtOut.Text = $cfg[""OutputFolder""]
Style-TextBox $txtOut
$pnlConfig.Controls.Add($txtOut)

$btnBrowseOut = New-Object System.Windows.Forms.Button
$btnBrowseOut.Text = ""Sfoglia""
$btnBrowseOut.SetBounds(536, 160, 88, 30)
$pnlConfig.Controls.Add($btnBrowseOut)
Style-SecondaryButton $btnBrowseOut
$btnBrowseOut.Add_Click({
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    if (Test-Path -LiteralPath $txtOut.Text) { $dlg.SelectedPath = $txtOut.Text }
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $txtOut.Text = $dlg.SelectedPath }
})

$lblInfo = New-Object System.Windows.Forms.Label
$lblInfo.Text = ""La scansione confronta i file STEP di input con gli STL gia' presenti in output, senza aprire NX. Se non trova conflitti la conversione parte subito.""
$lblInfo.SetBounds(24, 198, 600, 40)
Style-SubLabel $lblInfo
$pnlConfig.Controls.Add($lblInfo)

$cardCartelle.SendToBack()

$chkAdvanced = New-Object System.Windows.Forms.CheckBox
$chkAdvanced.Text = ""Mostra opzioni avanzate (tolleranze STL, superfici non chiuse)""
$chkAdvanced.SetBounds(24, 254, 500, 24)
Style-CheckBox $chkAdvanced
$pnlConfig.Controls.Add($chkAdvanced)

$panelAdv = New-Object System.Windows.Forms.Panel
$panelAdv.SetBounds(24, 284, 610, 140)
$panelAdv.Visible = $false
$panelAdv.BackColor = $ClrCardBg
$panelAdv.Region = New-RoundedRegion $panelAdv.Width $panelAdv.Height 12
$pnlConfig.Controls.Add($panelAdv)

$lblChordal = New-Object System.Windows.Forms.Label
$lblChordal.Text = ""Tolleranza chordal:""
$lblChordal.SetBounds(14, 14, 160, 20)
Style-Label $lblChordal
$panelAdv.Controls.Add($lblChordal)

$numChordal = New-Object System.Windows.Forms.NumericUpDown
$numChordal.SetBounds(184, 12, 100, 24)
$numChordal.DecimalPlaces = 4
$numChordal.Increment = 0.0005
$numChordal.Minimum = 0.0001
$numChordal.Maximum = 10
$numChordal.Value = [decimal](Parse-Double $cfg[""ChordalTol""] 0.0025)
Style-NumericUpDown $numChordal
$panelAdv.Controls.Add($numChordal)

$lblAdj = New-Object System.Windows.Forms.Label
$lblAdj.Text = ""Tolleranza adjacency:""
$lblAdj.SetBounds(14, 46, 160, 20)
Style-Label $lblAdj
$panelAdv.Controls.Add($lblAdj)

$numAdj = New-Object System.Windows.Forms.NumericUpDown
$numAdj.SetBounds(184, 44, 100, 24)
$numAdj.DecimalPlaces = 3
$numAdj.Increment = 0.01
$numAdj.Minimum = 0.001
$numAdj.Maximum = 100
$numAdj.Value = [decimal](Parse-Double $cfg[""AdjacencyTol""] 0.08)
Style-NumericUpDown $numAdj
$panelAdv.Controls.Add($numAdj)

$lblAng = New-Object System.Windows.Forms.Label
$lblAng.Text = ""Tolleranza angular:""
$lblAng.SetBounds(14, 78, 160, 20)
Style-Label $lblAng
$panelAdv.Controls.Add($lblAng)

$numAng = New-Object System.Windows.Forms.NumericUpDown
$numAng.SetBounds(184, 76, 100, 24)
$numAng.DecimalPlaces = 1
$numAng.Increment = 0.5
$numAng.Minimum = 0.1
$numAng.Maximum = 90
$numAng.Value = [decimal](Parse-Double $cfg[""AngularTol""] 5.0)
Style-NumericUpDown $numAng
$panelAdv.Controls.Add($numAng)

$chkExportOpen = New-Object System.Windows.Forms.CheckBox
$chkExportOpen.Text = ""Esporta anche i corpi non chiusi (superfici aperte)""
$chkExportOpen.SetBounds(14, 108, 480, 22)
$chkExportOpen.Checked = ($cfg[""ExportNotClosed""] -ne ""0"")
Style-CheckBox $chkExportOpen
$panelAdv.Controls.Add($chkExportOpen)

$chkAdvanced.Add_CheckedChanged({ $panelAdv.Visible = $chkAdvanced.Checked })

$btnScanConfig = New-Object System.Windows.Forms.Button
$btnScanConfig.Text = ""Avvia scansione""
$btnScanConfig.SetBounds(24, 456, 210, 44)
$pnlConfig.Controls.Add($btnScanConfig)
Style-PrimaryButton $btnScanConfig

$btnCancelConfig = New-Object System.Windows.Forms.Button
$btnCancelConfig.Text = ""Annulla""
$btnCancelConfig.SetBounds(466, 456, 210, 44)
$pnlConfig.Controls.Add($btnCancelConfig)
Style-SecondaryButton $btnCancelConfig

# --- Pannello Attesa (spinner mostrato mentre NX/il journal calcola la fase
# successiva: scansione conflitti, avvio conversione, riepilogo finale) ------
$pnlWaiting = New-Object System.Windows.Forms.Panel
$pnlWaiting.SetBounds(0, 0, 420, 220)
$pnlWaiting.BackColor = $ClrWindowBg
$form.Controls.Add($pnlWaiting)

$lblWaitTitle = New-Object System.Windows.Forms.Label
$lblWaitTitle.Text = ""Elaborazione in corso...""
$lblWaitTitle.SetBounds(24, 26, 372, 30)
Style-TitleLabel $lblWaitTitle
$pnlWaiting.Controls.Add($lblWaitTitle)

$lblWaitSub = New-Object System.Windows.Forms.Label
$lblWaitSub.Text = """"
$lblWaitSub.SetBounds(24, 62, 372, 40)
Style-SubLabel $lblWaitSub
$pnlWaiting.Controls.Add($lblWaitSub)

$pnlWaitSpinner = New-Object System.Windows.Forms.Panel
$pnlWaitSpinner.SetBounds(190, 120, 40, 40)
$pnlWaitSpinner.BackColor = $ClrWindowBg
$pnlWaiting.Controls.Add($pnlWaitSpinner)

$script:waitSpinnerAngle = 0
$pnlWaitSpinner.Add_Paint({
    param($spSender, $spEvent)
    $spEvent.Graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $spPen = New-Object System.Drawing.Pen($ClrAccent, 4)
    $spPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $spPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $spRect = New-Object System.Drawing.Rectangle(4, 4, ($spSender.Width - 8), ($spSender.Height - 8))
    $spEvent.Graphics.DrawArc($spPen, $spRect, $script:waitSpinnerAngle, 120)
    $spPen.Dispose()
})
$waitSpinnerTimer = New-Object System.Windows.Forms.Timer
$waitSpinnerTimer.Interval = 20
$waitSpinnerTimer.Add_Tick({
    $script:waitSpinnerAngle = ($script:waitSpinnerAngle + 8) % 360
    $pnlWaitSpinner.Invalidate()
})
$waitSpinnerTimer.Start()

function Show-Waiting($subText) {
    $lblWaitSub.Text = $subText
    $pnlConfig.Visible = $false
    $pnlWaiting.Visible = $true
    $pnlDecision.Visible = $false
    $pnlProgress.Visible = $false
    $pnlSummary.Visible = $false
    $form.Text = ""Elaborazione in corso...""
    $form.AcceptButton = $null
    $form.CancelButton = $null
    Center-Form $form 420 220
}

$btnScanConfig.Add_Click({
    $out = @{}
    $out[""Cancelled""] = ""0""
    $out[""InputFolder""] = $txtIn.Text
    $out[""OutputFolder""] = $txtOut.Text
    $out[""ChordalTol""] = $numChordal.Value.ToString($ic)
    $out[""AdjacencyTol""] = $numAdj.Value.ToString($ic)
    $out[""AngularTol""] = $numAng.Value.ToString($ic)
    $out[""ExportNotClosed""] = if ($chkExportOpen.Checked) { ""1"" } else { ""0"" }
    Write-KeyValueFile (Join-Path $WorkDir ""config_output.txt"") $out
    Show-Waiting ""Scansione dei conflitti in corso...""
})

$btnCancelConfig.Add_Click({
    Write-KeyValueFile (Join-Path $WorkDir ""config_output.txt"") @{ ""Cancelled"" = ""1"" }
    $form.Close()
})

# --- Pannello Decisione (mostrato solo se la scansione trova conflitti) ----
$pnlDecision = New-Object System.Windows.Forms.Panel
$pnlDecision.SetBounds(0, 0, 660, 520)
$pnlDecision.BackColor = $ClrWindowBg
$pnlDecision.Visible = $false
$form.Controls.Add($pnlDecision)

$lblTitleDecision = New-Object System.Windows.Forms.Label
$lblTitleDecision.Text = ""Trovati conflitti - come procedere?""
$lblTitleDecision.SetBounds(24, 20, 600, 30)
Style-TitleLabel $lblTitleDecision
$pnlDecision.Controls.Add($lblTitleDecision)

$cardSummary = Add-Card $pnlDecision 14 64 620 350

$txtSummary = New-Object System.Windows.Forms.TextBox
$txtSummary.Multiline = $true
$txtSummary.ReadOnly = $true
$txtSummary.TabStop = $false
$txtSummary.ScrollBars = ""Vertical""
$txtSummary.SetBounds(24, 74, 600, 330)
Style-TextBox $txtSummary
$pnlDecision.Controls.Add($txtSummary)

$cardSummary.SendToBack()

$btnOverwrite = New-Object System.Windows.Forms.Button
$btnOverwrite.Text = ""Sovrascrivi""
$btnOverwrite.SetBounds(24, 424, 185, 42)
$pnlDecision.Controls.Add($btnOverwrite)
Style-PrimaryButton $btnOverwrite

$btnCopy = New-Object System.Windows.Forms.Button
$btnCopy.Text = ""Copia in nuova cartella""
$btnCopy.SetBounds(222, 424, 205, 42)
$pnlDecision.Controls.Add($btnCopy)
Style-SecondaryButton $btnCopy

$btnStop = New-Object System.Windows.Forms.Button
$btnStop.Text = ""Interrompi""
$btnStop.SetBounds(440, 424, 184, 42)
$pnlDecision.Controls.Add($btnStop)
Style-SecondaryButton $btnStop

$btnOverwrite.Add_Click({
    Write-KeyValueFile (Join-Path $WorkDir ""decision_output.txt"") @{ ""Decision"" = ""Overwrite"" }
    Show-Waiting ""Avvio della conversione...""
})
$btnCopy.Add_Click({
    Write-KeyValueFile (Join-Path $WorkDir ""decision_output.txt"") @{ ""Decision"" = ""Copy"" }
    Show-Waiting ""Avvio della conversione...""
})
$btnStop.Add_Click({
    Write-KeyValueFile (Join-Path $WorkDir ""decision_output.txt"") @{ ""Decision"" = ""Stop"" }
    $form.Close()
})

# --- Pannello Avanzamento ----------------------------------------------------
$pnlProgress = New-Object System.Windows.Forms.Panel
$pnlProgress.SetBounds(0, 0, 560, 350)
$pnlProgress.BackColor = $ClrWindowBg
$pnlProgress.Visible = $false
$form.Controls.Add($pnlProgress)

$lblTitleProgress = New-Object System.Windows.Forms.Label
$lblTitleProgress.Text = ""Conversione STEP -> STL in corso""
$lblTitleProgress.SetBounds(24, 20, 260, 30)
Style-TitleLabel $lblTitleProgress
$pnlProgress.Controls.Add($lblTitleProgress)

# Anteprima del pezzo che si sta esportando: il journal C# (dentro NX)
# esporta la vista corrente direttamente su file JPG (nessun Bitmap
# coinvolto li'); qui, in PowerShell, System.Drawing e' sempre coerente
# con System.Windows.Forms, quindi possiamo tranquillamente caricarlo
# in una PictureBox e ricaricarlo ad ogni tick del poll timer.
$picPreview = New-Object System.Windows.Forms.PictureBox
$picPreview.SetBounds(400, 16, 136, 102)
$picPreview.BorderStyle = ""FixedSingle""
$picPreview.BackColor = $ClrCardBg
$picPreview.SizeMode = ""Zoom""
$pnlProgress.Controls.Add($picPreview)

$lblFile = New-Object System.Windows.Forms.Label
$lblFile.Text = ""Preparazione...""
$lblFile.SetBounds(24, 60, 360, 20)
$lblFile.AutoEllipsis = $true
Style-Label $lblFile
$pnlProgress.Controls.Add($lblFile)

$lblElapsed = New-Object System.Windows.Forms.Label
$lblElapsed.Text = ""Tempo trascorso: 00:00:00""
$lblElapsed.SetBounds(24, 84, 360, 20)
Style-SubLabel $lblElapsed
$pnlProgress.Controls.Add($lblElapsed)

# Spinner circolare animato: qui (a differenza del journal C# dentro
# NX) System.Windows.Forms e System.Drawing sono sempre la stessa
# coppia coerente, quindi un Panel disegnato a mano con Graphics/Pen
# funziona senza i problemi incontrati lato NX.
$pnlSpinner = New-Object System.Windows.Forms.Panel
$pnlSpinner.SetBounds(400, 128, 40, 40)
$pnlSpinner.BackColor = $ClrWindowBg
$pnlProgress.Controls.Add($pnlSpinner)

$script:spinnerAngle = 0
$pnlSpinner.Add_Paint({
    param($spSender, $spEvent)
    $spEvent.Graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $spPen = New-Object System.Drawing.Pen($ClrAccent, 4)
    $spPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $spPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $spRect = New-Object System.Drawing.Rectangle(4, 4, ($spSender.Width - 8), ($spSender.Height - 8))
    $spEvent.Graphics.DrawArc($spPen, $spRect, $script:spinnerAngle, 120)
    $spPen.Dispose()
})

$progressSpinnerTimer = New-Object System.Windows.Forms.Timer
$progressSpinnerTimer.Interval = 20
$progressSpinnerTimer.Add_Tick({
    $script:spinnerAngle = ($script:spinnerAngle + 8) % 360
    $pnlSpinner.Invalidate()
})

$pnlTrack = New-Object System.Windows.Forms.Panel
$pnlTrack.SetBounds(24, 176, 496, 14)
$pnlTrack.BackColor = $ClrTrack
$pnlTrack.Region = New-RoundedRegion $pnlTrack.Width $pnlTrack.Height 7
$pnlProgress.Controls.Add($pnlTrack)

$pnlFill = New-Object System.Windows.Forms.Panel
$pnlFill.SetBounds(0, 0, 2, 14)
$pnlFill.BackColor = $ClrAccent
$pnlFill.Region = New-RoundedRegion 2 14 7
$pnlTrack.Controls.Add($pnlFill)

$lblCount = New-Object System.Windows.Forms.Label
$lblCount.Text = ""0 di 0 completati""
$lblCount.SetBounds(24, 200, 300, 20)
Style-SubLabel $lblCount
$pnlProgress.Controls.Add($lblCount)

$lblStats = New-Object System.Windows.Forms.Label
$lblStats.Text = """"
$lblStats.SetBounds(24, 222, 496, 20)
Style-SubLabel $lblStats
$pnlProgress.Controls.Add($lblStats)

$btnCancelProgress = New-Object System.Windows.Forms.Button
$btnCancelProgress.Text = ""Annulla""
$btnCancelProgress.SetBounds(370, 258, 150, 40)
$pnlProgress.Controls.Add($btnCancelProgress)
Style-SecondaryButton $btnCancelProgress

$script:cancelRequested = $false
$btnCancelProgress.Add_Click({
    $script:cancelRequested = $true
    $btnCancelProgress.Enabled = $false
    $btnCancelProgress.Text = ""Annullamento...""
    if (-not [string]::IsNullOrEmpty($script:progressOutFolder)) {
        try {
            if (-not (Test-Path -LiteralPath $script:progressOutFolder)) { New-Item -ItemType Directory -Path $script:progressOutFolder -Force | Out-Null }
            Set-Content -LiteralPath (Join-Path $script:progressOutFolder ""CANCEL.txt"") -Value ""cancel"" -Encoding UTF8
        } catch {
        }
    }
})

# Due timer separati: uno ""lento"" (poll) legge il file di stato scritto
# dal batch e memorizza solo il target da raggiungere; uno ""veloce""
# (render) anima la barra avvicinandola gradualmente al target ad ogni
# tick, invece di farla scattare di colpo alla nuova percentuale. Quando
# l'animazione raggiunge il target finale (Done=1 ricevuto), il render timer
# NON chiude piu' la finestra: la fa passare al pannello ""Attesa"" in vista
# del riepilogo finale, che il journal C# scrivera' a breve.
$script:targetWidth = 2
$script:doneReceived = $false
$script:previewLastWrite = [DateTime]::MinValue
$script:progressTotalInitial = 0
$script:progressStatusFile = """"
$script:previewPath = """"
$script:progressOutFolder = """"

$progressPollTimer = New-Object System.Windows.Forms.Timer
$progressPollTimer.Interval = 300
$progressPollTimer.Add_Tick({
    $ts = (Get-Date) - $script:progressStartTime
    $lblElapsed.Text = (""Tempo trascorso: {0:D2}:{1:D2}:{2:D2}"" -f [int]$ts.TotalHours, $ts.Minutes, $ts.Seconds)

    # Ricarica la preview solo se il file e' cambiato dall'ultima
    # lettura. ReadAllBytes + MemoryStream (invece di Image]::FromFile)
    # evita di tenere il file JPG bloccato: il lato NX deve poter
    # sovrascriverlo liberamente al giro successivo. Un fallimento
    # (es. file colto a meta' scrittura) si ritenta semplicemente al
    # prossimo tick, senza mai interrompere la conversione.
    if (Test-Path -LiteralPath $script:previewPath) {
        try {
            $fi = Get-Item -LiteralPath $script:previewPath
            if ($fi.LastWriteTimeUtc -ne $script:previewLastWrite) {
                $imgBytes = [System.IO.File]::ReadAllBytes($script:previewPath)
                $imgStream = New-Object System.IO.MemoryStream(,$imgBytes)
                $newImg = [System.Drawing.Image]::FromStream($imgStream)
                $oldImg = $picPreview.Image
                $picPreview.Image = $newImg
                if ($oldImg) { $oldImg.Dispose() }
                $script:previewLastWrite = $fi.LastWriteTimeUtc
            }
        } catch {
        }
    }

    $st = Read-KeyValueFile $script:progressStatusFile
    if ($st.Count -eq 0) { return }

    $cur = 0
    $tot = $script:progressTotalInitial
    $okCount = 0
    $failCount = 0
    [int]::TryParse($st[""Current""], [ref]$cur) | Out-Null
    [int]::TryParse($st[""Total""], [ref]$tot) | Out-Null
    [int]::TryParse($st[""Ok""], [ref]$okCount) | Out-Null
    [int]::TryParse($st[""Failed""], [ref]$failCount) | Out-Null
    $fileName = $st[""FileName""]

    $totForPct = $tot
    if ($totForPct -le 0) { $totForPct = 1 }
    $pct = [double]$cur / [double]$totForPct
    if ($pct -lt 0) { $pct = 0 }
    if ($pct -gt 1) { $pct = 1 }

    $newTarget = [int]($pnlTrack.Width * $pct)
    if ($newTarget -lt 2) { $newTarget = 2 }
    if ($newTarget -gt $pnlTrack.Width) { $newTarget = $pnlTrack.Width }
    $script:targetWidth = $newTarget

    if ([string]::IsNullOrEmpty($fileName)) {
        $lblFile.Text = ""Elaborazione in corso...""
    } else {
        $lblFile.Text = (""In elaborazione: {0}"" -f $fileName)
    }
    $lblCount.Text = (""{0} di {1} completati ({2}%)"" -f $cur, $tot, [int]($pct * 100))
    $lblStats.Text = (""Riusciti: {0}    Falliti: {1}"" -f $okCount, $failCount)

    if ($st[""Done""] -eq ""1"") {
        $script:doneReceived = $true
    }
})

$progressRenderTimer = New-Object System.Windows.Forms.Timer
$progressRenderTimer.Interval = 30
$progressRenderTimer.Add_Tick({
    $currentWidth = $pnlFill.Width
    $delta = $script:targetWidth - $currentWidth
    if ([Math]::Abs($delta) -gt 0) {
        $step = [int]($delta * 0.3)
        if ($step -eq 0) { $step = if ($delta -gt 0) { 1 } else { -1 } }
        $newWidth = $currentWidth + $step
        if ($newWidth -lt 2) { $newWidth = 2 }
        if ($newWidth -gt $pnlTrack.Width) { $newWidth = $pnlTrack.Width }
        $pnlFill.Width = $newWidth
        $pnlFill.Region = New-RoundedRegion $newWidth $pnlFill.Height 7
    }

    if ($script:doneReceived -and $pnlFill.Width -ge $script:targetWidth) {
        $progressPollTimer.Stop()
        $progressRenderTimer.Stop()
        $progressSpinnerTimer.Stop()
        if ($picPreview.Image) { $picPreview.Image.Dispose(); $picPreview.Image = $null }
        Show-Waiting ""Preparazione del riepilogo...""
    }
})

# --- Pannello Riepilogo finale -----------------------------------------------
$pnlSummary = New-Object System.Windows.Forms.Panel
$pnlSummary.SetBounds(0, 0, 620, 460)
$pnlSummary.BackColor = $ClrWindowBg
$pnlSummary.Visible = $false
$form.Controls.Add($pnlSummary)

$lblTitleSummary = New-Object System.Windows.Forms.Label
$lblTitleSummary.Text = ""Conversione completata""
$lblTitleSummary.SetBounds(24, 20, 560, 30)
Style-TitleLabel $lblTitleSummary
$pnlSummary.Controls.Add($lblTitleSummary)

$cardFinal = Add-Card $pnlSummary 14 64 580 300

$txtFinal = New-Object System.Windows.Forms.TextBox
$txtFinal.Multiline = $true
$txtFinal.ReadOnly = $true
$txtFinal.TabStop = $false
$txtFinal.ScrollBars = ""Vertical""
$txtFinal.SetBounds(24, 74, 560, 280)
Style-TextBox $txtFinal
$pnlSummary.Controls.Add($txtFinal)

$cardFinal.SendToBack()

$btnOpen = New-Object System.Windows.Forms.Button
$btnOpen.Text = ""Apri cartella di output""
$btnOpen.SetBounds(24, 368, 230, 42)
$pnlSummary.Controls.Add($btnOpen)
Style-SecondaryButton $btnOpen
$script:summaryOutFolder = """"
$btnOpen.Add_Click({
    if (Test-Path -LiteralPath $script:summaryOutFolder) { Start-Process -FilePath ""explorer.exe"" -ArgumentList @($script:summaryOutFolder) }
})

$btnClose = New-Object System.Windows.Forms.Button
$btnClose.Text = ""Chiudi""
$btnClose.SetBounds(414, 368, 170, 42)
$pnlSummary.Controls.Add($btnClose)
Style-PrimaryButton $btnClose
$btnClose.Add_Click({ $form.Close() })

# --- Orchestratore: sorveglia ""next_stage.txt"", scritto dal journal C# per
# dire alla finestra (attualmente sul pannello Attesa) quale pannello
# mostrare quando ha finito di calcolare la fase successiva. -----------------
$script:nextStagePath = Join-Path $WorkDir ""next_stage.txt""

$orchTimer = New-Object System.Windows.Forms.Timer
$orchTimer.Interval = 200
$orchTimer.Add_Tick({
    if (-not (Test-Path -LiteralPath $script:nextStagePath)) { return }
    $cmd = Read-KeyValueFile $script:nextStagePath
    try { Remove-Item -LiteralPath $script:nextStagePath -Force -ErrorAction SilentlyContinue } catch {}
    $stageName = $cmd[""Stage""]

    if ($stageName -eq ""Decision"") {
        $decisionInputFile = Join-Path $WorkDir ""decision_input.txt""
        $summaryText = """"
        if (Test-Path -LiteralPath $decisionInputFile) {
            $summaryText = [string]::Join([Environment]::NewLine, (Get-Content -LiteralPath $decisionInputFile -Encoding UTF8))
        }
        $txtSummary.Text = $summaryText

        $pnlConfig.Visible = $false
        $pnlWaiting.Visible = $false
        $pnlProgress.Visible = $false
        $pnlSummary.Visible = $false
        $pnlDecision.Visible = $true
        $form.Text = ""Trovati conflitti - come procedere?""
        $form.AcceptButton = $null
        $form.CancelButton = $null
        Center-Form $form 660 520
    }
    elseif ($stageName -eq ""Progress"") {
        $progMeta = Read-KeyValueFile (Join-Path $WorkDir ""progress_meta.txt"")
        $script:progressOutFolder = $progMeta[""OutputFolder""]
        $script:progressStatusFile = Join-Path $WorkDir ""progress_status.txt""
        $script:previewPath = Join-Path $WorkDir ""preview.jpg""
        $script:previewLastWrite = [DateTime]::MinValue
        $script:progressTotalInitial = 0
        [int]::TryParse($progMeta[""Total""], [ref]$script:progressTotalInitial) | Out-Null

        $lblFile.Text = ""Preparazione...""
        $lblElapsed.Text = ""Tempo trascorso: 00:00:00""
        $lblCount.Text = (""0 di {0} completati"" -f $script:progressTotalInitial)
        $lblStats.Text = """"
        $pnlFill.Width = 2
        $pnlFill.Region = New-RoundedRegion 2 $pnlFill.Height 7
        if ($picPreview.Image) { $picPreview.Image.Dispose(); $picPreview.Image = $null }

        $script:progressStartTime = Get-Date
        $script:targetWidth = 2
        $script:doneReceived = $false
        $script:cancelRequested = $false
        $btnCancelProgress.Enabled = $true
        $btnCancelProgress.Text = ""Annulla""

        $pnlConfig.Visible = $false
        $pnlWaiting.Visible = $false
        $pnlDecision.Visible = $false
        $pnlSummary.Visible = $false
        $pnlProgress.Visible = $true
        $form.Text = ""Conversione in corso...""
        $form.AcceptButton = $null
        $form.CancelButton = $null
        Center-Form $form 560 350

        $progressPollTimer.Start()
        $progressRenderTimer.Start()
        $progressSpinnerTimer.Start()
    }
    elseif ($stageName -eq ""Summary"") {
        $summaryInputFile = Join-Path $WorkDir ""summary_input.txt""
        $summaryText2 = """"
        if (Test-Path -LiteralPath $summaryInputFile) {
            $summaryText2 = [string]::Join([Environment]::NewLine, (Get-Content -LiteralPath $summaryInputFile -Encoding UTF8))
        }
        $summaryMeta = Read-KeyValueFile (Join-Path $WorkDir ""summary_meta.txt"")
        $script:summaryOutFolder = $summaryMeta[""OutputFolder""]
        $txtFinal.Text = $summaryText2

        $pnlConfig.Visible = $false
        $pnlWaiting.Visible = $false
        $pnlDecision.Visible = $false
        $pnlProgress.Visible = $false
        $pnlSummary.Visible = $true
        $form.Text = ""Conversione completata""
        $form.AcceptButton = $btnClose
        $form.CancelButton = $null
        Center-Form $form 620 460
    }
})
$orchTimer.Start()

# --- Avvio: mostra subito il pannello Config, poi resta aperta per tutta la
# durata del run (le fasi successive sono solo cambi di pannello, mai una
# nuova finestra). ------------------------------------------------------------
$pnlConfig.Visible = $true
$form.AcceptButton = $btnScanConfig
$form.CancelButton = $btnCancelConfig
Center-Form $form 720 580
Enable-FadeIn $form

# Senza questo, il primo campo di testo riceve il focus all'apertura
# e Windows ne seleziona automaticamente tutto il contenuto (il
# testo appare ""sempre evidenziato in blu""). Sposto solo il cursore
# a inizio testo, senza selezionare nulla.
$form.Add_Shown({ $txtIn.Select(0, 0) })

[void]$form.ShowDialog()

$orchTimer.Stop()
$orchTimer.Dispose()
$waitSpinnerTimer.Stop()
$waitSpinnerTimer.Dispose()
$progressPollTimer.Stop()
$progressPollTimer.Dispose()
$progressRenderTimer.Stop()
$progressRenderTimer.Dispose()
$progressSpinnerTimer.Stop()
$progressSpinnerTimer.Dispose()
if ($picPreview.Image) { $picPreview.Image.Dispose() }
";

    // Percorso PRIMARIO: mostra la GUI vera lanciando UN SOLO processo
    // powershell.exe, separato da quello di NX (vedi il changelog v12 in
    // cima al file per il perche'), che resta vivo per l'intera durata del
    // run e cambia semplicemente pannello al suo interno (vedi il commento
    // in testa a ExternalGuiScriptSource) - mai un nuovo processo/finestra
    // per fase. Gestisce, in ordine: schermata di configurazione (cartelle +
    // opzioni avanzate), scansione preventiva, ed EVENTUALMENTE (solo se la
    // scansione trova conflitti) la schermata di decisione. Qualunque
    // eccezione qui dentro (powershell.exe non trovato, processo che non
    // produce il file di output atteso, ecc.) risale a Main(), che la
    // intercetta e passa al fallback - stessa logica di degradazione
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

        externalGuiProcess = StartPersistentExternalGui(scriptPath, workDir);

        // Da qui in poi la finestra persistente resta aperta finche' non la
        // si chiude esplicitamente (Annulla/Interrompi/Chiudi) o finche' il
        // run non finisce: se una qualunque eccezione risale da qui (es. un
        // errore durante la scansione preventiva), la finestra andrebbe
        // altrimenti lasciata bloccata per sempre sul pannello "Attesa" -
        // il try/catch la chiude esplicitamente prima di propagare
        // l'eccezione a Main() (che passa al fallback).
        try
        {
            string configOutputPath = Path.Combine(workDir, "config_output.txt");
            WaitForFileOrProcessExit(externalGuiProcess, configOutputPath, "Config");
            Dictionary<string, string> configOutput = ReadKeyValueFile(configOutputPath);

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

            // Da qui in poi la finestra e' gia' sul pannello "Attesa" (l'ha
            // mostrato lei stessa subito dopo aver scritto config_output.txt):
            // la scansione sotto avviene mentre l'utente la vede semplicemente
            // girare, senza alcuna finestra che si chiude o riappare.
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
            SignalNextStage(workDir, "Decision");

            string decisionOutputPath = Path.Combine(workDir, "decision_output.txt");
            WaitForFileOrProcessExit(externalGuiProcess, decisionOutputPath, "Decision");
            Dictionary<string, string> decisionOutput = ReadKeyValueFile(decisionOutputPath);
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
        catch (Exception)
        {
            TryCloseExternalGuiProcess();
            throw;
        }
    }

    // Chiude (best-effort) la finestra persistente della GUI esterna quando
    // qualcosa e' andato storto altrove e nessuno le direbbe piu' quale
    // pannello mostrare dopo: senza questo resterebbe visibile per sempre
    // sul pannello "Attesa", con la rotella che gira a vuoto. Un fallimento
    // qui non deve mai mascherare l'errore originale.
    private static void TryCloseExternalGuiProcess()
    {
        if (externalGuiProcess == null)
        {
            return;
        }
        try
        {
            if (externalGuiProcess.HasExited)
            {
                return;
            }
            externalGuiProcess.CloseMainWindow();
            if (!externalGuiProcess.WaitForExit(2000))
            {
                externalGuiProcess.Kill();
            }
        }
        catch (Exception)
        {
            // best-effort: la pulizia della finestra non deve mai nascondere l'errore originale
        }
    }

    // Best-effort: mostra il riepilogo finale nella GUI esterna (testo +
    // pulsante "Apri cartella di output"), nella STESSA finestra gia' aperta
    // per tutto il run (nessun nuovo processo/finestra). Se qualcosa va
    // storto qui la conversione e' comunque gia' completata e il suo esito
    // e' gia' nel log e nella Listing Window: un fallimento in questo passo
    // va solo loggato, non deve mai far sembrare fallita la conversione
    // stessa.
    private static void TryShowExternalGuiSummary(ListingWindow lw, BatchResult result)
    {
        if (string.IsNullOrEmpty(externalGuiWorkDir) || string.IsNullOrEmpty(externalGuiScriptPath)
            || externalGuiProcess == null || externalGuiProcess.HasExited)
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

            SignalNextStage(externalGuiWorkDir, "Summary");

            // Blocca finche' l'utente non chiude la finestra di riepilogo
            // (stesso comportamento sincrono delle versioni precedenti, solo
            // che qui non c'e' un nuovo processo da avviare: e' lo stesso
            // gia' in esecuzione dall'inizio del run).
            externalGuiProcess.WaitForExit();
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

    // Avvia l'UNICO processo powershell.exe che ospitera' la GUI esterna per
    // l'intera durata del run (vedi il commento in testa a
    // ExternalGuiScriptSource): non e' piu' un launcher sincrono per singola
    // fase, ma un processo di lunga durata, avviato una sola volta qui e mai
    // riavviato. -WindowStyle Hidden + CreateNoWindow nascondono la console
    // di PowerShell (che qui non serve, e' solo un launcher): la finestra
    // WinForms creata dallo script rimane comunque visibile normalmente, non
    // essendo legata alla visibilita' della console. -ExecutionPolicy Bypass
    // vale solo per QUESTO singolo processo (non cambia alcuna policy di
    // sistema/utente) e non richiede diritti di amministratore.
    private static Process StartPersistentExternalGui(string scriptPath, string workDir)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "powershell.exe";
        psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" +
            scriptPath + "\" -WorkDir \"" + workDir + "\"";
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        return Process.Start(psi);
    }

    // Attende che la GUI esterna scriva il file di risposta atteso per la
    // fase corrente (l'utente ha cliccato un pulsante in quel pannello),
    // controllando periodicamente anche che il processo non sia terminato
    // nel frattempo (es. l'utente ha chiuso la finestra con la X, o
    // PowerShell e' crashato): in tal caso il file non arrivera' mai, quindi
    // si segnala subito l'errore invece di restare in attesa per sempre.
    private static void WaitForFileOrProcessExit(Process process, string expectedFile, string stageName)
    {
        while (!File.Exists(expectedFile))
        {
            if (process.HasExited)
            {
                throw new Exception("La GUI esterna si e' chiusa senza produrre il file di risposta atteso per lo stage " + stageName + ".");
            }
            System.Threading.Thread.Sleep(150);
        }
    }

    // Scrive il "comando" che dice alla GUI esterna (attualmente ferma sul
    // pannello di attesa con la rotella di caricamento) quale pannello
    // mostrare non appena questo lato C# ha finito di calcolare la fase
    // successiva (scansione conflitti completata, conversione avviata,
    // riepilogo pronto). Il file viene consumato (cancellato) dallo script
    // PowerShell non appena letto.
    private static void SignalNextStage(string workDir, string stage)
    {
        Dictionary<string, string> cmd = new Dictionary<string, string>();
        cmd["Stage"] = stage;
        WriteKeyValueFile(Path.Combine(workDir, "next_stage.txt"), cmd);
    }

    // Ultimo istante (UTC) in cui e' stata tentata una cattura di anteprima:
    // usato per non esportare un'immagine ad ogni singolo componente aperto
    // durante un assieme con centinaia di parti (costoso e inutile - basta
    // aggiornare la preview circa 2 volte al secondo).
    private static DateTime lastPreviewCaptureUtc = DateTime.MinValue;
    private static readonly TimeSpan MinPreviewCaptureInterval = TimeSpan.FromMilliseconds(500);

    // Esporta la vista corrente direttamente su file JPG tramite l'API nativa
    // di NX (ImageExportBuilder): NESSUN tipo di System.Drawing.Common
    // (Bitmap/Image/Graphics) e' coinvolto qui, il file finisce scritto su
    // disco da NX stesso. Lo stage "Progress" della GUI esterna (PowerShell,
    // dove System.Windows.Forms e System.Drawing sono sempre una coppia
    // coerente) lo rilegge periodicamente e lo mostra in una PictureBox.
    // Scrive prima su un file temporaneo e poi lo copia sul percorso finale,
    // cosi' il lato PowerShell non legge mai un JPG a meta' scritto.
    // Best-effort e silenzioso: un fallimento qui non deve MAI interrompere
    // o rallentare la conversione, che e' la parte che conta davvero.
    private static void CaptureAndDisplayScreenshot(Session theSession)
    {
        if (!enableProgressPreview)
        {
            return;
        }
        if (string.IsNullOrEmpty(externalGuiWorkDir))
        {
            return;
        }
        if (DateTime.UtcNow - lastPreviewCaptureUtc < MinPreviewCaptureInterval)
        {
            return;
        }
        lastPreviewCaptureUtc = DateTime.UtcNow;

        try
        {
            Part displayPart = theSession.Parts.Display;
            if (displayPart == null)
            {
                return;
            }

            string finalPath = Path.Combine(externalGuiWorkDir, "preview.jpg");
            string tempPath = Path.Combine(externalGuiWorkDir, "preview_tmp_" + Guid.NewGuid().ToString("N") + ".jpg");

            NXOpen.Gateway.ImageExportBuilder imageExportBuilder = displayPart.Views.CreateImageExportBuilder();
            try
            {
                imageExportBuilder.RegionMode = false;
                imageExportBuilder.DeviceWidth = 272;
                imageExportBuilder.DeviceHeight = 204;
                imageExportBuilder.FileFormat = NXOpen.Gateway.ImageExportBuilder.FileFormats.Jpg;
                imageExportBuilder.FileName = tempPath;
                imageExportBuilder.BackgroundOption = NXOpen.Gateway.ImageExportBuilder.BackgroundOptions.Original;
                imageExportBuilder.EnhanceEdges = false;
                imageExportBuilder.Commit();
            }
            finally
            {
                imageExportBuilder.Destroy();
            }

            if (File.Exists(tempPath))
            {
                File.Copy(tempPath, finalPath, true);
                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("CaptureAndDisplayScreenshot failed: " + ex.Message);
        }
    }

    // Scrive lo stato di avanzamento corrente nel formato chiave=valore che lo
    // stage "Progress" della GUI esterna legge periodicamente (vedi il timer
    // nello script PowerShell). Best-effort: un fallimento qui (es. file
    // temporaneamente bloccato) non deve mai interrompere la conversione,
    // che e' la parte che conta davvero.
    private static void WriteProgressStatus(string path, int current, int total, string fileName, int ok, int failed, bool done)
    {
        try
        {
            Dictionary<string, string> status = new Dictionary<string, string>();
            status["Current"] = current.ToString(CultureInfo.InvariantCulture);
            status["Total"] = total.ToString(CultureInfo.InvariantCulture);
            status["FileName"] = fileName ?? "";
            status["Ok"] = ok.ToString(CultureInfo.InvariantCulture);
            status["Failed"] = failed.ToString(CultureInfo.InvariantCulture);
            status["Done"] = done ? "1" : "0";
            WriteKeyValueFile(path, status);
        }
        catch (Exception)
        {
            // aggiornamento di avanzamento best-effort: un fallimento qui non deve fermare la conversione
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

    // Chiude il log incrementale e lo riscrive completamente solo come rete di
    // sicurezza quando lo stream non e' mai partito o ha segnalato un errore.
    private static void WriteFinalLogSafety()
    {
        bool mustRewriteLog = !incrementalLogHealthy;
        if (logWriter != null)
        {
            try { logWriter.Flush(); }
            catch (Exception) { mustRewriteLog = true; }
            try { logWriter.Dispose(); }
            catch (Exception) { mustRewriteLog = true; }
            logWriter = null;
        }

        // Se lo stream incrementale ha scritto correttamente tutte le righe,
        // il file e' gia' completo: riscriverlo da zero qui raddoppiava l'I/O
        // proprio alla fine del batch, soprattutto su share/antivirus.
        if (!mustRewriteLog)
        {
            return;
        }

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
        incrementalLogHealthy = false;
        try
        {
            logWriter = new StreamWriter(logFilePath, false, new UTF8Encoding(false));
            foreach (string existingLine in logLines)
            {
                logWriter.WriteLine(existingLine);
            }
            logWriter.Flush();
            incrementalLogHealthy = true;
        }
        catch (Exception)
        {
            // se non riusciamo nemmeno ad aprirlo, Log() ripiega da sola su
            // File.AppendAllText riga per riga; resta comunque la scrittura
            // finale di sicurezza in WriteFinalLogSafety().
            logWriter = null;
            incrementalLogHealthy = false;
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

        // Finestra di avanzamento con barra di progresso "live": mostrata solo
        // se la GUI esterna (PowerShell) e' quella in uso per questo run (vedi
        // RunExternalGuiFlow), riusando la STESSA finestra/processo gia'
        // aperto dall'inizio del run (nessun nuovo processo qui): le si dice
        // solo di passare al pannello "Avanzamento" (SignalNextStage), che
        // poi legge periodicamente il file di stato scritto ad ogni file
        // STEP processato (vedi WriteProgressStatus) e torna da solo al
        // pannello di attesa quando vi legge Done=1. Qualunque problema qui
        // e' non fatale: la conversione procede comunque, semplicemente
        // senza la finestra di avanzamento.
        bool showProgressWindow = !string.IsNullOrEmpty(externalGuiWorkDir)
            && !string.IsNullOrEmpty(externalGuiScriptPath)
            && externalGuiProcess != null && !externalGuiProcess.HasExited
            && stepFiles.Count > 0;
        string progressStatusPath = null;
        if (showProgressWindow)
        {
            try
            {
                progressStatusPath = Path.Combine(externalGuiWorkDir, "progress_status.txt");
                Dictionary<string, string> progressMeta = new Dictionary<string, string>();
                progressMeta["OutputFolder"] = outputFolder;
                progressMeta["Total"] = stepFiles.Count.ToString(CultureInfo.InvariantCulture);
                WriteKeyValueFile(Path.Combine(externalGuiWorkDir, "progress_meta.txt"), progressMeta);
                WriteProgressStatus(progressStatusPath, 0, stepFiles.Count, "", 0, 0, false);
                SignalNextStage(externalGuiWorkDir, "Progress");
            }
            catch (Exception)
            {
                showProgressWindow = false;
            }
        }

        try
        {
        for (int i = 0; i < stepFiles.Count; i++)
        {
            if (showProgressWindow)
            {
                WriteProgressStatus(progressStatusPath, i, stepFiles.Count, Path.GetFileNameWithoutExtension(stepFiles[i]), ok, failed, false);
            }

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

                // OpenActiveDisplay con AllowAdditional imposta la parte aperta come
                // "Display" ma non sempre anche come "Work" (tipicamente capita per il
                // primo file aperto nella sessione, quando non c'e' ancora una Work part):
                // ApplicationSwitchImmediate pero' richiede una Work part valida, quindi
                // va impostata esplicitamente se OpenActiveDisplay non l'ha gia' fatto.
                Part workPart = theSession.Parts.Work;
                if (workPart == null)
                {
                    workPart = basePart1 as Part;
                    if (workPart != null)
                    {
                        theSession.Parts.SetWork(workPart);
                    }
                }
                if (workPart == null)
                {
                    throw new Exception(
                        "Impossibile aprire il file STEP come parte di lavoro (nessuna Work part disponibile dopo OpenActiveDisplay): " + stepFile);
                }
                theSession.ApplicationSwitchImmediate("UG_APP_MODELING");

                CaptureAndDisplayScreenshot(theSession);

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
                try { theSession.CleanUpFacetedFacesAndEdges(); }
                catch (Exception exCleanup)
                {
                    Log(lw, "  -> Avviso: errore durante la pulizia delle faccette: " + exCleanup.Message);
                }
                try { theSession.Parts.CloseAll(NXOpen.BasePart.CloseModified.CloseModified, null); }
                catch (Exception exClose)
                {
                    Log(lw, "  -> Avviso: errore durante la chiusura della parte: " + exClose.Message);
                }
                FlushIncrementalLog();
            }

            Log(lw, "");
        }
        }
        finally
        {
            if (showProgressWindow)
            {
                // Segnala Done=1: il pannello di avanzamento, gia' aperto
                // nella stessa finestra persistente, se ne accorge da solo
                // (poll timer) e passa al pannello di attesa in vista del
                // riepilogo finale - nessun processo da attendere qui.
                WriteProgressStatus(progressStatusPath, ok + failed, stepFiles.Count, "", ok, failed, true);
            }
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

            // Vedi commento equivalente sull'apertura dello STEP: AllowAdditional non
            // garantisce che la parte aperta diventi anche la Work part, che invece
            // serve ad ApplicationSwitchImmediate.
            Part workPart = theSession.Parts.Work;
            if (workPart == null)
            {
                workPart = basePart1 as Part;
                if (workPart != null)
                {
                    theSession.Parts.SetWork(workPart);
                }
            }
            if (workPart == null)
            {
                throw new Exception(
                    "Impossibile aprire il file .prt come parte di lavoro (nessuna Work part disponibile dopo OpenActiveDisplay): " + prtPath);
            }
            theSession.ApplicationSwitchImmediate("UG_APP_MODELING");

            CaptureAndDisplayScreenshot(theSession);

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
            try { theSession.CleanUpFacetedFacesAndEdges(); }
            catch (Exception) { /* best-effort: la chiusura va comunque tentata */ }
            try { theSession.Parts.CloseAll(NXOpen.BasePart.CloseModified.CloseModified, null); }
            catch (Exception)
            {
                // se anche la chiusura fallisce, si prosegue comunque con il componente successivo
            }
            FlushIncrementalLog();
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
    // La pulizia delle faccette viene eseguita dal chiamante una sola volta per
    // parte, subito prima della chiusura, non una volta per corpo o gruppo.
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
                if (logWriter != null)
                {
                    logWriter.WriteLine(message);
                }
                else
                {
                    File.AppendAllText(logFilePath, message + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                incrementalLogHealthy = false;
                // se anche l'append fallisce, il messaggio resta comunque in
                // logLines e verra' ritentato nella scrittura finale.
            }
        }

    }

    // Mantiene il log recuperabile durante il batch senza forzare un flush per
    // ogni singola riga. Il chiamante lo invoca al confine sicuro tra due STEP.
    private static void FlushIncrementalLog()
    {
        if (logWriter == null)
        {
            return;
        }
        try
        {
            logWriter.Flush();
        }
        catch (Exception)
        {
            incrementalLogHealthy = false;
        }
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
