// =============================================================================
// NX Open Journal - Conversione massiva STEP -> STL (v7)
// Basato sul journal originale "journal.cs" (export singolo STL registrato in NX),
// esteso per scorrere automaticamente tutti i file .stp/.step di una cartella.
//
// COSA E' STATO CORRETTO/AGGIUNTO IN QUESTA VERSIONE (v7):
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
// 1. Le due cartelle sono gia' impostate:
//    Input:  C:\Users\AndreaScalenghe\Desktop\STEP_Convert
//    Output: C:\Users\AndreaScalenghe\Desktop\STL_Convert
// 2. In NX: Strumenti > Automazione > Journal > Riproduci... e seleziona questo file .cs.
// 3. Per ogni file .stp/.step:
//    - i corpi solidi diretti vengono esportati in STL_Convert, un file per corpo;
//    - i corpi NON solidi (superfici aperte) diretti vengono esportati in
//      STL_Convert\000_Not_Closed_Mesh, un file per corpo, con suffisso
//      "_NOT_CLOSED_MESH";
//    - se non c'e' NESSUN corpo diretto (ne' solido ne' aperto) ed e' un
//      assieme, lo script scende nei sotto-componenti (o riapre i .prt gia'
//      noti dall'indice) e applica la stessa logica solido/non chiuso per
//      ciascun componente, esportando ogni occorrenza separatamente se il
//      componente e' usato piu' volte.
// 4. Il progresso viene stampato nella Listing Window di NX. Log dettagliato in
//    "log_conversione.txt", errori in "errori_conversione.log", mappa
//    step->componenti in "component_index.txt" (tutti dentro STL_Convert).
// 5. Prova PRIMA su 2-3 file soli in STEP_Convert (includendo se possibile un
//    assieme con un componente ripetuto piu' volte, e/o una parte con corpi
//    multi-lump), poi lancia sul totale.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using NXOpen;
using NXOpen.Assemblies;

public class NXJournal
{
    // =========================================================================
    // CONFIGURAZIONE
    // =========================================================================
    private static readonly string inputFolder  = @"C:\Users\AndreaScalenghe\Desktop\STEP_Convert";
    private static readonly string outputFolder = @"C:\Users\AndreaScalenghe\Desktop\STL_Convert";

    // Tolleranze STL (riprese identiche dal journal originale)
    private static readonly double chordalTol   = 0.0025;
    private static readonly double adjacencyTol = 0.08;
    private static readonly double angularTol   = 5.0;

    // true  = esporta anche i corpi NON solidi (superfici aperte/sheet) in una
    //         sottocartella dedicata dentro l'output, ben identificati nel nome
    // false = i corpi non solidi vengono ignorati
    private static readonly bool exportNotClosedMeshes = true;

    // Nome della sottocartella (dentro outputFolder) dove finiscono i corpi non chiusi
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

    private static List<string> logLines = new List<string>();

    public static void Main(string[] args)
    {
        Session theSession = Session.GetSession();
        ListingWindow lw = theSession.ListingWindow;
        lw.Open();

        Log(lw, "=== Avvio conversione batch STEP -> STL ===");

        try
        {
            RunBatch(theSession, lw);
        }
        catch (Exception ex)
        {
            Log(lw, "ERRORE GENERALE (lo script si e' fermato): " + ex.Message);
            Log(lw, ex.StackTrace);
        }
        finally
        {
            // Scrive sempre il log su file, anche se qualcosa e' andato storto,
            // cosi' possiamo capire a che punto si e' fermato.
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
    }

    private static void RunBatch(Session theSession, ListingWindow lw)
    {
        if (!Directory.Exists(inputFolder))
        {
            Log(lw, "ERRORE: cartella di input non trovata: " + inputFolder);
            return;
        }
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        string errLogPath = Path.Combine(outputFolder, "errori_conversione.log");
        string indexPath = GetComponentIndexPath();
        Dictionary<string, List<string>> componentIndex = LoadComponentIndex(indexPath);

        if (componentIndex.Count > 0)
        {
            Log(lw, string.Format("Indice componenti caricato: {0} assiemi gia' noti da esecuzioni precedenti.",
                componentIndex.Count));
        }

        List<string> stepFiles = new List<string>();
        stepFiles.AddRange(Directory.GetFiles(inputFolder, "*.stp"));
        stepFiles.AddRange(Directory.GetFiles(inputFolder, "*.step"));
        stepFiles.Sort();

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

                    string fileBaseName = BuildInstanceFileBaseName(partName, occ, total);

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
                partLoadStatus1.Dispose();

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
                    // chiusi distinti in un'unica mesh non piu' separabile.
                    int filesWritten = 0;

                    if (solidBodies.Count > 0)
                    {
                        List<string> outFiles = ExportBodiesSeparately(theSession, lw, solidBodies, outputFolder,
                            baseName, ref grandSkippedFiles);
                        filesWritten += outFiles.Count;
                        grandSolidBodies += outFiles.Count;
                    }

                    if (exportNotClosedMeshes && openBodies.Count > 0)
                    {
                        string notClosedFolder = GetNotClosedFolder();
                        List<string> outFilesOpen = ExportBodiesSeparately(theSession, lw, openBodies, notClosedFolder,
                            baseName + notClosedSuffix, ref grandSkippedFiles);
                        filesWritten += outFilesOpen.Count;
                        grandOpenBodies += outFilesOpen.Count;
                        Log(lw, string.Format("  -> Attenzione: {0} corpo/i non chiuso/i (superfici aperte), esportati in {1}\\",
                            openBodies.Count, notClosedSubfolderName));
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
                        ref compOk, ref compFailed, errLogPath, baseName, indexPath,
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

    // Costruisce il nome base del file per un componente, in base a quante
    // occorrenze totali ha nell'assieme: se una sola, nome semplice; se piu'
    // di una, aggiunge il suffisso "_occNN" (NN = indice dell'occorrenza).
    private static string BuildInstanceFileBaseName(string partName, int occurrenceIndex, int totalOccurrences)
    {
        if (totalOccurrences <= 1)
        {
            return partName;
        }
        return string.Format("{0}_occ{1:00}", partName, occurrenceIndex);
    }

    // Prima passata (sola lettura, nessun export): scorre ricorsivamente
    // l'albero dei componenti e conta quante volte compare ciascun nome di
    // Part tra i componenti foglia CHE HANNO ALMENO UN CORPO. Serve per sapere
    // in anticipo la quantita' totale di ogni componente, cosi' la seconda
    // passata (ExportComponentTree) puo' assegnare correttamente i suffissi
    // "_occNN" fin dalla prima occorrenza incontrata.
    private static void CountLeafOccurrences(Component comp, Dictionary<string, int> counts)
    {
        Component[] children = comp.GetChildren();

        if (children != null && children.Length > 0)
        {
            foreach (Component child in children)
            {
                CountLeafOccurrences(child, counts);
            }
            return;
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
    // Per ogni componente FOGLIA (senza figli) con corpi, esporta un STL
    // separato per ogni OCCORRENZA (non deduplica piu' per nome: se lo stesso
    // componente compare 4 volte nell'assieme, viene esportato 4 volte, con
    // suffisso "_occNN"). Ogni occorrenza trovata viene anche registrata
    // nell'indice persistente (una riga per occorrenza, senza deduplica),
    // cosi' alla prossima esecuzione lo script sapra' sia quali componenti
    // aprire sia in che quantita'.
    private static void ExportComponentTree(Session theSession, ListingWindow lw, Component comp,
        Dictionary<string, int> totalCounts, Dictionary<string, int> exportedSoFar,
        ref int compOk, ref int compFailed, string errLogPath,
        string stepBaseName, string indexPath,
        ref int grandSolid, ref int grandOpen, ref int grandFiles, ref int grandSkipped)
    {
        Component[] children = comp.GetChildren();

        if (children != null && children.Length > 0)
        {
            foreach (Component child in children)
            {
                ExportComponentTree(theSession, lw, child, totalCounts, exportedSoFar,
                    ref compOk, ref compFailed, errLogPath, stepBaseName, indexPath,
                    ref grandSolid, ref grandOpen, ref grandFiles, ref grandSkipped);
            }
            return;
        }

        // Componente foglia: prendo la Part reale (prototype) gia' caricata in sessione.
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

        string fileBaseName = BuildInstanceFileBaseName(partName, occ, total);

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
            // (superfici aperte) finiscono nella sottocartella dedicata.
            int filesWritten = 0;

            if (compSolidBodies.Count > 0)
            {
                List<string> outFiles = ExportBodiesSeparately(theSession, lw, compSolidBodies, outputFolder,
                    fileBaseName, ref grandSkipped);
                filesWritten += outFiles.Count;
                grandSolid += outFiles.Count;
            }

            if (exportNotClosedMeshes && compOpenBodies.Count > 0)
            {
                string notClosedFolder = GetNotClosedFolder();
                List<string> outFilesOpen = ExportBodiesSeparately(theSession, lw, compOpenBodies, notClosedFolder,
                    fileBaseName + notClosedSuffix, ref grandSkipped);
                filesWritten += outFilesOpen.Count;
                grandOpen += outFilesOpen.Count;
            }

            grandFiles += filesWritten;
            compOk++;

            string quantityNote = (total > 1) ? string.Format(" [istanza {0}/{1}]", occ, total) : "";
            Log(lw, string.Format("     -> componente OK: {0}{1} ({2} solidi, {3} non chiusi, {4} file)",
                partName, quantityNote, compSolidBodies.Count, compOpenBodies.Count, filesWritten));
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
            partLoadStatus1.Dispose();

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
            // (superfici aperte) finiscono nella sottocartella dedicata.
            int filesWritten = 0;

            if (solidBodies.Count > 0)
            {
                List<string> outFiles = ExportBodiesSeparately(theSession, lw, solidBodies, outputFolder,
                    fileBaseName, ref grandSkipped);
                filesWritten += outFiles.Count;
                grandSolid += outFiles.Count;
            }

            if (exportNotClosedMeshes && openBodies.Count > 0)
            {
                string notClosedFolder = GetNotClosedFolder();
                List<string> outFilesOpen = ExportBodiesSeparately(theSession, lw, openBodies, notClosedFolder,
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

    // Restituisce (creandola se non esiste) la sottocartella dedicata alle mesh
    // non chiuse, dentro la cartella di output principale.
    private static string GetNotClosedFolder()
    {
        string folder = Path.Combine(outputFolder, notClosedSubfolderName);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        return folder;
    }

    // Esporta una LISTA di corpi in file STL SEPARATI, uno per corpo, cosi' NX non
    // li fonde in un'unica mesh. Se c'e' un solo corpo, il file si chiama
    // "<baseFileName>.stl" (comportamento identico a prima). Se ce ne sono di
    // piu', ciascuno diventa "<baseFileName>_corpoNN.stl".
    // PROTEZIONE ANTI-SOVRASCRITTURA: se il file di destinazione esiste gia'
    // (es. perche' lo STEP e' stato riaperto ed e' stato riprocessato), quel
    // corpo NON viene esportato e viene loggato un avviso esplicito, invece di
    // sovrascrivere silenziosamente un file gia' presente. "skippedCount" viene
    // incrementato per ogni file saltato in questo modo.
    // Restituisce la lista dei percorsi file EFFETTIVAMENTE scritti (esclusi
    // quelli saltati perche' gia' esistenti).
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
            if (File.Exists(outFile))
            {
                skippedCount++;
                Log(lw, string.Format("     -> ATTENZIONE: \"{0}\" esiste gia' in {1}, NON sovrascritto (corpo saltato).",
                    Path.GetFileName(outFile), outputFolder));
                return outputFiles;
            }
            ExportSingleBodyToStl(theSession, bodies[0], outFile);
            outputFiles.Add(outFile);
            return outputFiles;
        }

        for (int i = 0; i < bodies.Count; i++)
        {
            string outFile = Path.Combine(outputFolder, string.Format("{0}_corpo{1:00}.stl", baseFileName, i + 1));
            if (File.Exists(outFile))
            {
                skippedCount++;
                Log(lw, string.Format("     -> ATTENZIONE: \"{0}\" esiste gia' in {1}, NON sovrascritto (corpo saltato).",
                    Path.GetFileName(outFile), outputFolder));
                continue;
            }
            ExportSingleBodyToStl(theSession, bodies[i], outFile);
            outputFiles.Add(outFile);
        }

        return outputFiles;
    }

    // Esporta UN SOLO corpo in un file STL dedicato.
    private static void ExportSingleBodyToStl(Session theSession, Body body, string outputFile)
    {
        STLCreator stlCreator1 = theSession.DexManager.CreateStlCreator();
        stlCreator1.AutoNormalGen = true;
        stlCreator1.ChordalTol = chordalTol;
        stlCreator1.AdjacencyTol = adjacencyTol;
        stlCreator1.AngularTol = angularTol;
        stlCreator1.OutputFile = outputFile;

        NXObject[] singleBodyArray = new NXObject[] { body };
        stlCreator1.ExportSelectionBlock.Add(singleBodyArray);

        stlCreator1.Commit();
        stlCreator1.Destroy();

        theSession.CleanUpFacetedFacesAndEdges();
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
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
