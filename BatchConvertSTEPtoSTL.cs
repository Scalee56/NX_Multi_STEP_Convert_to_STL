// =============================================================================
// NX Open Journal - Conversione massiva STEP -> STL (v2 - fix crash)
// Basato sul journal originale "journal.cs" (export singolo STL registrato in NX),
// esteso per scorrere automaticamente tutti i file .stp/.step di una cartella.
//
// COSA E' STATO CORRETTO IN QUESTA VERSIONE:
// - Il tipo usato per chiudere le parti era sbagliato (NXOpen.BasePartCloseModified
//   invece di NXOpen.BasePart.CloseModified, che e' un enum ANNIDATO dentro BasePart).
//   Questo probabilmente mandava in crash/errore la compilazione del journal in NX.
// - Rimosso l'uso di LINQ (Where/Cast/ToArray) per ridurre al minimo eventuali
//   incompatibilita' con la versione di .NET usata dal compilatore journal della tua NX.
// - Aggiunto un try/catch generale attorno a tutto il ciclo, che scrive SEMPRE un log
//   su file (anche se la Listing Window non si apre o non si vede), cosi' se qualcosa
//   va ancora storto abbiamo comunque un file da leggere per capire dove si e' fermato.
//
// ISTRUZIONI D'USO:
// 1. Le due cartelle sono gia' impostate:
//    Input:  C:\Users\AndreaScalenghe\Desktop\STEP_Convert
//    Output: C:\Users\AndreaScalenghe\Desktop\STL_Convert
// 2. In NX: Strumenti > Automazione > Journal > Riproduci... e seleziona questo file .cs.
// 3. Lo script apre un file .stp alla volta, esporta TUTTI i corpi solidi trovati nella
//    parte in un .stl con lo stesso nome del file di origine, chiude la parte senza
//    salvare e passa al successivo.
// 4. Il progresso viene stampato nella Listing Window di NX. Un log dettagliato viene
//    scritto anche su file in "log_conversione.txt" dentro la cartella di output.
// 5. Prova PRIMA su 2-3 file soli in STEP_Convert, poi lancia sul totale.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using NXOpen;

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

    // true  = esporta solo i corpi solidi (consigliato per la stampa/sinterizzazione)
    // false = esporta tutti i corpi presenti, incluse eventuali superfici
    private static readonly bool onlySolidBodies = true;

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

        List<string> stepFiles = new List<string>();
        stepFiles.AddRange(Directory.GetFiles(inputFolder, "*.stp"));
        stepFiles.AddRange(Directory.GetFiles(inputFolder, "*.step"));
        stepFiles.Sort();

        Log(lw, string.Format("Trovati {0} file STEP da convertire in: {1}", stepFiles.Count, inputFolder));
        Log(lw, string.Format("Output STL in: {0}", outputFolder));
        Log(lw, "");

        int ok = 0;
        int failed = 0;

        for (int i = 0; i < stepFiles.Count; i++)
        {
            string stepFile = stepFiles[i];
            string baseName = Path.GetFileNameWithoutExtension(stepFile);
            Log(lw, string.Format("[{0}/{1}] {2}", i + 1, stepFiles.Count, baseName));

            try
            {
                PartLoadStatus partLoadStatus1;
                BasePart basePart1 = theSession.Parts.OpenActiveDisplay(
                    stepFile, DisplayPartOption.AllowAdditional, out partLoadStatus1);
                partLoadStatus1.Dispose();

                Part workPart = theSession.Parts.Work;
                theSession.ApplicationSwitchImmediate("UG_APP_MODELING");
                theSession.CleanUpFacetedFacesAndEdges();

                List<NXObject> exportObjects = new List<NXObject>();
                foreach (Body b in workPart.Bodies)
                {
                    if (!onlySolidBodies || b.IsSolidBody)
                    {
                        exportObjects.Add(b);
                    }
                }

                if (exportObjects.Count == 0)
                {
                    throw new Exception("Nessun corpo" + (onlySolidBodies ? " solido" : "") + " trovato nella parte.");
                }

                STLCreator stlCreator1 = theSession.DexManager.CreateStlCreator();
                stlCreator1.AutoNormalGen = true;
                stlCreator1.ChordalTol = chordalTol;
                stlCreator1.AdjacencyTol = adjacencyTol;
                stlCreator1.AngularTol = angularTol;
                stlCreator1.OutputFile = Path.Combine(outputFolder, baseName + ".stl");

                stlCreator1.ExportSelectionBlock.Add(exportObjects.ToArray());

                stlCreator1.Commit();
                stlCreator1.Destroy();

                theSession.CleanUpFacetedFacesAndEdges();

                ok++;
                Log(lw, string.Format("  -> OK ({0} corpi esportati)", exportObjects.Count));
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
                // Chiude tutte le parti aperte senza salvare, prima di passare al file successivo.
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
        }

        Log(lw, "");
        Log(lw, "=====================================================");
        Log(lw, string.Format("Completato: {0} riusciti, {1} falliti su {2} totali.", ok, failed, stepFiles.Count));
        if (failed > 0)
        {
            Log(lw, "Dettagli errori in: " + errLogPath);
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
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
