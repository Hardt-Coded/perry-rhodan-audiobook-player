#r "netstandard"
#r "System.Xml.Linq"
//#r @"C:\Users\Dieselmeister\.nuget\packages\fsharp.data\3.0.0\lib\netstandard2.0\FSharp.Data.dll"
#r @"C:\Users\Dieselmeister\.nuget\packages\fsharp.data\3.0.0\lib\netstandard2.0\FSharp.Data.dll"



open FSharp.Data
open System
open System.Text.RegularExpressions

[<Literal>]
let htmlSample = """<div id=\"downloads\"><h2>Meine Hörbücher</h2><h4><a href=\"#oeffne\" onclick=\"openCat(0)\">PERRY RHODAN > Bonus (3 Downloads)</a></h4><ul id=\"cat0\" style=\"display:none;\"><li>Perry Rhodan Bonus - Zweittod (<a href=\"/index.php?id=16&productID=1609534\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1609534&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1609534&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan 2000: Die ES-Chroniken (Download) (<a href=\"/index.php?id=16&productID=2146857\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=2146857&productFileTypeID=2\">Multitrack</a> / 
<a href=\"butler.php?action=audio&productID=2146857&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan 1500: Ruf der Unsterblichkeit (Download) (<a href=\"/index.php?id=16&productID=2555019\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=2555019&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=2555019&productFileTypeID=3\">Onetrack</a> </li></ul><h4><a href=\"#oeffne\" onclick=\"openCat(1)\">PERRY RHODAN > Hörbücher Erstauflage > Ab Nr. 1800 (21 Downloads)</a></h4><ul id=\"cat1\" style=\"display:none;\"><li>Perry Rhodan Nr. 1819: Eine Ladung Vivoc (Download) (<a href=\"/index.php?id=16&productID=1437213\">ansehen</a>) 
- <a href=\"butler.php?action=audio&productID=1437213&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1437213&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1818: Testfall Lafayette (Download)  (<a href=\"/index.php?id=16&productID=1415862\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1415862&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1415862&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1817: Krieger der Gazkar (Download)  (<a href=\"/index.php?id=16&productID=1417304\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1417304&productFileTypeID=2\">Multitrack</a> 
/ <a href=\"butler.php?action=audio&productID=1417304&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1816: Hüter der Glückseligkeit (Download)  (<a href=\"/index.php?id=16&productID=1415861\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1415861&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1415861&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1815: Rätselwelt Galorn (Download)  (<a href=\"/index.php?id=16&productID=1437212\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1437212&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1437212&productFileTypeID=3\">Onetrack
</a> </li><li>Perry Rhodan Nr. 1814: Unter dem Galornenstern (Download)  (<a href=\"/index.php?id=16&productID=1273965\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1273965&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1273965&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1813: Die Mörder von Bröhnder (Download)  (<a href=\"/index.php?id=16&productID=1262103\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1262103&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1262103&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1812: Camelot (Download)  
(<a href=\"/index.php?id=16&productID=1322907\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1322907&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1322907&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1811: Konferenz der Galaktiker (Download)  (<a href=\"/index.php?id=16&productID=1239129\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1239129&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1239129&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1810: Der Weg nach Camelot (Download) (<a href=\"/index.php?id=16&productID=1239128\">ansehen</a>) 
- <a href=\"butler.php?action=audio&productID=1239128&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1239128&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1809: Hetzjagd durch den Hyperraum (Download)  (<a href=\"/index.php?id=16&productID=1176841\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1176841&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1176841&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1808: Landung auf Lafayette (Download)  (<a href=\"/index.php?id=16&productID=1176840\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1176840&productFileTypeID=2\">
Multitrack</a> / <a href=\"butler.php?action=audio&productID=1176840&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1807: Die Haut des Bösen (Download)  (<a href=\"/index.php?id=16&productID=1176839\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1176839&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1176839&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1806: Der Mutant der Cantrell (Download) (<a href=\"/index.php?id=16&productID=1176838\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1176838&productFileTypeID=2\">Multitrack</a> / 
<a href=\"butler.php?action=audio&productID=1176838&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1805: Arsenal der Macht (Download)  (<a href=\"/index.php?id=16&productID=1176837\">ansehen</a>) -<a href=\"butler.php?action=audio&productID=1176837&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1176837&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1804: Kampf ums Überleben (Download)  (<a href=\"/index.php?id=16&productID=1176836\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1176836&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1176836&productFileTypeID=3\">
Onetrack</a> </li><li>Perry Rhodan Nr. 1803: Der Riese Schimbaa (Download)  (<a href=\"/index.php?id=16&productID=1176835\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1176835&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1176835&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1802: Stiefkinder der Sonne (Download) (<a href=\"/index.php?id=16&productID=1176834\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1176834&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1176834&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1801: Die Herreach (Download) 
(<a href=\"/index.php?id=16&productID=1176833\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=1176833&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=1176833&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1800: Zeitraffer (Download) (<a href=\"/index.php?id=16&productID=570664\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=570664&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=570664&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 1800: Zeitraffer (Download) (<a href=\"/index.php?id=16&productID=570664\">ansehen</a>) - 
<a href=\"butler.php?action=audio&productID=570664&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=570664&productFileTypeID=3\">Onetrack</a> </li></ul><h4><a href=\"#oeffne\" onclick=\"openCat(2)\">PERRY RHODAN > Hörbücher Erstauflage > Ab Nr. 2400 (19 Downloads)</a></h4><ul id=\"cat2\" style=\"display:none;\"><li>Perry Rhodan Nr. 2499: Das Opfer (Download) (<a href=\"/index.php?id=16&productID=27552\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=27552&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=27552&productFileTypeID=3\">Onetrack</a> </li>
<li>Perry Rhodan Nr. 2497: Das Monokosmium (Download) (<a href=\"/index.php?id=16&productID=27485\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=27485&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=27485&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2496: Chaotender gegen Sol (Download) (<a href=\"/index.php?id=16&productID=27431\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=27431&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=27431&productFileTypeID=3\">Onetrack</a> </li>
<li>Perry Rhodan Nr. 2495: Koltorocs Feuer (Download) (<a href=\"/index.php?id=16&productID=27391\">ansehen</a>) -<a href=\"butler.php?action=audio&productID=27391&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=27391&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2493: Der Weltweise (Download) (<a href=\"/index.php?id=16&productID=27153\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=27153&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=27153&productFileTypeID=3\">Onetrack</a> </li>
<li>Perry Rhodan Nr. 2491: Der dritte Messenger (Download) (<a href=\"/index.php?id=16&productID=26911\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26911&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26911&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2490: Die dunklen Gärten (Download) (<a href=\"/index.php?id=16&productID=26876\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26876&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26876&productFileTypeID=3\">
Onetrack</a> </li><li>Perry Rhodan Nr. 2489: Schach dem Chaos (Download) (<a href=\"/index.php?id=16&productID=26819\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26819&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26819&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2488: Hinter dem Kernwall (Download) (<a href=\"/index.php?id=16&productID=26668\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26668&productFileTypeID=2\">Multitrack</a> / 
<a href=\"butler.php?action=audio&productID=26668&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2487: Die String-Legaten (Download) (<a href=\"/index.php?id=16&productID=26648\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26648&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26648&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2486: Wispern des Hyperraums (Download) (<a href=\"/index.php?id=16&productID=26549\">ansehen</a>) - 
<a href=\"butler.php?action=audio&productID=26549&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26549&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2483: Die Nadel des Chaos (Download) (<a href=\"/index.php?id=16&productID=26244\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26244&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26244&productFileTypeID=3\">Onetrack</a> </li>
<li>Perry Rhodan Nr. 2482: Der ewige Kerker (Download) (<a href=\"/index.php?id=16&productID=26218\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26218&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26218&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2481: Günstlinge des Hyperraums (Download) (<a href=\"/index.php?id=16&productID=26183\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26183&product
FileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26183&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2480: Die Prognostiker (Download) (<a href=\"/index.php?id=16&productID=26116\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26116&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26116&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2479: Technomorphose (Download) (<a href
=\"/index.php?id=16&productID=26032\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=26032&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=26032&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2475: Opfergang (Download) (<a href=\"/index.php?id=16&productID=25505\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=25505&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=
25505&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2471: Das Geschenk der Metaläufer (Download) (<a href=\"/index.php?id=16&productID=25356\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=25356&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=25356&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2446: Die Negane Stadt (Download) (<a href=\"/index.php?id=16&productID=22775\">ansehen</a>) - 
<a href=\"butler.php?action=audio&productID=22775&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=22775&productFileTypeID=3\">Onetrack</a> </li></ul><h4><a href=\"#oeffne\" onclick=\"openCat(3)\">PERRY RHODAN > Hörbücher Erstauflage > Ab Nr. 2500 (61 Downloads)</a></h4><ul id=\"cat3\" style=\"display:none;\"><li>Perry Rhodan Nr. 2568: Einsatzkommando Infiltration (Download) (<a href=\"/index.php?id=16&productID=36005\">ansehen
</a>) - <a href=\"butler.php?action=audio&productID=36005&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=36005&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2567: Duell an der Schneise (Download) (<a href=\"/index.php?id=16&productID=35845\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=35845&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=35845&productFileTypeID
=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2565: Vastrears Odyssee (Download) (<a href=\"/index.php?id=16&productID=35686\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=35686&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=35686&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2564: Die verlorene Stimme (Download) (<a href=\"/index.php?id=16&productID=35412\">ansehen</a>) - <a href=\"butler.php?acti
on=audio&productID=35412&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=35412&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2561: Insel der goldenen Funken (Download) (<a href=\"/index.php?id=16&productID=35103\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=35103&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=35103&productFileTypeID=3\">Onetrack</a> </li><li>Pe
rry Rhodan Nr. 2560: Das Raunen des Vamu (Download) (<a href=\"/index.php?id=16&productID=35026\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=35026&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=35026&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2559: Splitter des Bösen (Download) (<a href=\"/index.php?id=16&productID=34969\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=34969&prod
uctFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=34969&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2558: Die Stadt am Ende des Weges (Download) (<a href=\"/index.php?id=16&productID=34946\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=34946&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=34946&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2557: Der Me
ntalpilot (Download) (<a href=\"/index.php?id=16&productID=34857\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=34857&productFileTypeID=2\">Multitrack</a> / <a href=\"butler.php?action=audio&productID=34857&productFileTypeID=3\">Onetrack</a> </li><li>Perry Rhodan Nr. 2556: Im Innern des Wunders (Download) (<a href=\"/index.php?id=16&productID=34817\">ansehen</a>) - <a href=\"butler.php?action=audio&productID=34817&productFileTypeID=2\">Multitrack
</a> / <a href=\"butler.php?action=audio&productID=34817&productFileTypeID=3\">Onetrack</a> </li></div>"""



//type DownloadSite = HtmlProvider< htmlSample >


let regexMatch pattern input =
    let res = Regex.Match(input,pattern)
    if res.Success then
        Some res.Value
    else
        None

let regexMatchOpt pattern input =
    input
    |> Option.bind (regexMatch pattern)
    

let regexMatchGroup pattern group input =
    let res = Regex.Match(input,pattern)
    if res.Success && res.Groups.Count >= group then
        Some res.Groups.[group].Value
    else
        None

let regexMatchGroupOpt pattern group input =
    input
    |> Option.bind (regexMatchGroup group pattern)
    


open System.IO

let tst = 
    use fs = new FileStream(@"D:\temp\perryRhodanApp\mega.txt",FileMode.Open)

    HtmlDocument.Load(fs).Descendants("div")
    //DownloadSite.Parse(htmlData).Html.Descendants("div")
    |> Seq.toArray 
    |> Array.filter (fun i -> i.AttributeValue("id") = "downloads")
    |> Array.tryHead
    |> Option.map (
        fun i -> 
            i.Descendants("li") 
            |> Seq.filter (fun i -> not (i.InnerText().Contains("Impressum"))) 
            |> Seq.filter (fun i -> i.Descendants("a") |> Seq.exists (fun i -> i.TryGetAttribute("href") |> Option.map(fun m -> m.Value().Contains("butler.php?action=audio")) |> Option.defaultValue false) )
            |> Seq.toArray
    )
    |> Option.defaultValue ([||])
    


let (|InvariantEqual|_|) (str:string) arg = 
    if String.Compare(str, arg, StringComparison.InvariantCultureIgnoreCase) = 0
    then Some() else None
let (|OrdinalEqual|_|) (str:string) arg = 
    if String.Compare(str, arg, StringComparison.OrdinalIgnoreCase) = 0
    then Some() else None
let (|InvariantContains|_|) (str:string) (arg:string) = 
    if arg.IndexOf(str, StringComparison.InvariantCultureIgnoreCase) > -1
    then Some() else None
let (|OrdinalContains|_|) (str:string) (arg:string) = 
    if arg.IndexOf(str, StringComparison.OrdinalIgnoreCase) > -1
    then Some() else None

let downloadNameRegex = Regex(@"([A-Za-z .-]*)(\d*)(:| - )([\w\säöüÄÖÜ.:!\-]*[\(\)Teil \d]*)(.*)(( - Multitrack \/ Onetrack)|( - Multitrack)|( - Onetrack))")

tst 
|> Seq.filter (fun i -> 
        match i.InnerText() with
        | InvariantContains "Multitrack" -> true
        | InvariantContains "Onetrack" -> true
        | _ -> false
    )
|> Seq.groupBy (fun i ->
        let innerText = i.InnerText()
        if not (downloadNameRegex.IsMatch(innerText)) then "Other"
        else
            let matchTitle = downloadNameRegex.Match(innerText)
            matchTitle.Groups.[1].Value.Replace("Nr.", "").Trim()
    )
|> Seq.map (fun (key,items) -> 
        key,
        items        
        |> Seq.map ( fun i ->
                let innerText = i.InnerText()
                let episodeNumber = 
                    if not (downloadNameRegex.IsMatch(innerText)) then None
                    else
                        let epNumRes = Int32.TryParse(downloadNameRegex.Match(innerText).Groups.[2].Value)
                        match epNumRes with
                        | true, x -> Some x
                        | _ -> None
                let episodeTitle = 
                    if not (downloadNameRegex.IsMatch(innerText)) then innerText.Trim()
                    else 
                        let ept = downloadNameRegex.Match(innerText).Groups.[4].Value.Trim()
                        ept.Substring(0,(ept.Length-2)).ToString().Trim()

                let linkForMultiDownload = 
                    i.Descendants["a"]
                    |> Seq.filter (fun i ->  i.Attribute("href").Value().ToLower().Contains("productfiletypeid=2"))
                    |> Seq.map (fun i -> i.Attribute("href").Value())
                    |> Seq.tryHead

                let linkProductSite = 
                    i.Descendants["a"]
                    |> Seq.filter (fun i -> i.InnerText() = "ansehen")
                    |> Seq.map (fun i -> i.Attribute("href").Value())
                    |> Seq.tryHead

                let productId = 
                    linkProductSite
                    |> regexMatchGroupOpt 2 "(productID=)(\d*)"
                    
                    

                (episodeNumber,episodeTitle,linkForMultiDownload,linkProductSite, productId, i)
            )
        |> Seq.sortBy (fun (epNumber,_,_,_,_,_) -> 
                match epNumber with
                | None -> -1
                | Some x -> x
            )
    )     
|> Seq.iter ( fun (key,items) -> 
        printfn "---- %s ----" key
        items |> Seq.iter (fun (epNumber,epTitel,link,plink,pid,i) -> printfn "%A - %s (%A) (%A) (%A)" epNumber epTitel link plink pid)
    )



[<Literal>]
let productSiteHtml = """

<div id="productdetail"><nav class="breadcrumb"><a href="/index.php?id=12&amp;categoryID=4774&amp;catalogID=851">Hörbuch-Downloads</a> » <a href="/index.php?id=12&amp;categoryID=4777&amp;catalogID=851">PERRY RHODAN</a> » <a href="/index.php?id=12&amp;categoryID=4896&amp;catalogID=851">Hörbücher PERRY RHODAN NEO</a></nav>
<div class="leftpane"><h2>Perry Rhodan Neo Nr. 189: Die Leiden des Androiden (Hörbuch-Download)</h2><form name="add" style="float:right" action="/caddy.php" method="post"><input name="productID" type="hidden" value="3688426"><input name="quantity" type="hidden" value="1"><input name="rewardFlag" type="hidden" value="0"><input name="action" type="hidden" value="add"><input name="pageID" type="hidden" value="50"><button class="buttonbuy" type="submit">in den Warenkorb</button></form>
<p class="pricetag">
<span class="price">9,95&nbsp;€</span><br>inkl. 19%&nbsp;MwSt. und<br> ggf. zzgl. Versand</p>
<p>Das Jahr 2058: Nach dramatischen Abenteuern in den Tiefen des Weltraums wollen sich Perry Rhodan und seine Gefährten auf die Erde und deren Probleme konzentrieren. Gemeinsam arbeiten die Menschen daran, die Verwüstungen vergangener Kriege und Katastrophen zu beseitigen. Die Terranische Union wächst weiter zusammen.
<br><br>
Dann jedoch dringen Außerirdische ins Sonnensystem ein. Sie können sich unsichtbar machen, deshalb nennen die Menschen sie Laurins. Hinter diesem Vorstoß steckt offenbar die geheimnisvolle Allianz, die seit Langem gegen die Erdbewohner kämpft.
<br><br>
Perry Rhodan spürt der Allianz bis zum Rand der Milchstraße nach, wo er auf eine übermächtige Kriegsflotte stößt. Befehlshaber der Bestien ist Masmer Tronkh, ein erbitterter Feind der Menschheit. Als Rhodan dessen unheilvollen Pläne vereiteln will, bedeutet dies auch das Ende für die Leiden des Androiden ...</p></div><div class="rightpane"><img title="Perry Rhodan Neo Nr. 189: Die Leiden des Androiden (Hörbuch-Download)" class="image" id="mainimg" alt="Perry Rhodan Neo Nr. 189: Die Leiden des Androiden (Hörbuch-Download)" src="/prod_images/prod_3668694_8365410_2.jpg" border="0">
                <div id="amazingaudioplayer-1" style="display:block;position:relative;width:300px;height:auto;margin:0px auto 0px;">
                    <ul class="amazingaudioplayer-audios" style="display:none;">
                        <li data-duration="0" data-image="" data-info="" data-album="" data-title="Perry Rhodan Neo Nr. 189: Die Leiden des Androiden (Hörbuch-Download)" data-artist="">
                            <div class="amazingaudioplayer-source" data-type="audio/mpeg" data-src="http://download.einsamedien.de.s3.amazonaws.com/storage/3688426/004_PRNEO_189_Die_Leiden_des_Androiden.mp3?AWSAccessKeyId=AKIAIAWFYTGZGQS2YDPA&amp;Expires=1547542722&amp;Signature=MeRDu5r6Myv2ZhcH1DQjTIvUkwo%3D">
                        </div></li>
                    </ul>
                <div class="amazingaudioplayer-bar"><div class="amazingaudioplayer-playpause" style="display: block;"><div class="amazingaudioplayer-play" style='background-position: left top; width: 24px; height: 24px; background-image: url("https://www.einsamedien.de/audioplayerengine/playpause-24-24-1.png"); background-repeat: no-repeat; display: block; cursor: pointer;'></div><div class="amazingaudioplayer-pause" style='background-position: right top; width: 24px; height: 24px; background-image: url("https://www.einsamedien.de/audioplayerengine/playpause-24-24-1.png"); background-repeat: no-repeat; display: none; cursor: pointer;'></div></div><div class="amazingaudioplayer-bar-title" style="width: 80px; height: auto; text-indent: -91px; overflow: hidden; display: block; white-space: nowrap;"><span class="amazingaudioplayer-bar-title-text">Perry Rhodan Neo Nr. 189: Die Leiden des Androiden (Hörbuch-Download)</span></div><div class="amazingaudioplayer-volume" style="display: block;"><div class="amazingaudioplayer-volume-button" style='background-position: left top; width: 24px; height: 24px; background-image: url("https://www.einsamedien.de/audioplayerengine/volume-24-24-1.png"); background-repeat: no-repeat; display: block; position: relative; cursor: pointer;'></div><div class="amazingaudioplayer-volume-bar" style="padding: 8px; left: 0px; width: 8px; height: 64px; bottom: 100%; display: none; position: absolute; box-sizing: content-box;"><div class="amazingaudioplayer-volume-bar-adjust" style="width: 100%; height: 100%; display: block; position: relative; cursor: pointer;"><div class="amazingaudioplayer-volume-bar-adjust-active" style="left: 0px; width: 100%; height: 100%; bottom: 0px; display: block; position: absolute;"></div></div></div></div><div class="amazingaudioplayer-time">00:00 / 06:05</div><div class="amazingaudioplayer-progress" style="height: 8px; overflow: hidden; display: block; cursor: pointer;"><div class="amazingaudioplayer-progress-loaded" style="left: 0px; top: 0px; width: 100%; height: 100%; display: block; position: absolute;"></div><div class="amazingaudioplayer-progress-played" style="left: 0px; top: 0px; width: 0%; height: 100%; display: block; position: absolute;"></div></div><div class="amazingaudioplayer-bar-buttons-clear"></div></div><div class="amazingaudioplayer-bar-clear"></div><audio preload="auto"><source src="http://download.einsamedien.de.s3.amazonaws.com/storage/3688426/004_PRNEO_189_Die_Leiden_des_Androiden.mp3?AWSAccessKeyId=AKIAIAWFYTGZGQS2YDPA&amp;Expires=1547542722&amp;Signature=MeRDu5r6Myv2ZhcH1DQjTIvUkwo%3D" type="audio/mpeg"></audio></div>
                <!-- div class="audioplayer">
                    <div id="audioplayer_1">
                        <script type="text/javascript">
                        AudioPlayer.embed("audioplayer_1", {
                            soundFile: "http%3A%2F%2Fdownload.einsamedien.de.s3.amazonaws.com%2Fstorage%2F3688426%2F004_PRNEO_189_Die_Leiden_des_Androiden.mp3%3FAWSAccessKeyId%3DAKIAIAWFYTGZGQS2YDPA%26Expires%3D1547542722%26Signature%3DMeRDu5r6Myv2ZhcH1DQjTIvUkwo%253D",
                            titles: "Perry Rhodan Neo Nr. 189: Die Leiden des Androiden (Hörbuch-Download)",
                            initialvolume: 75,
                            transparentpagebg: "yes",
                            autostart: "no",
                            animation: "yes"
                        });
                        </script>
                    </div>
                </div --><ul class="properties"><li><span class="label">Autor:</span> Rainer Schorm</li><li><span class="label">Sprecher:</span> Hanno Dinger</li><li><span class="label">Länge:</span> 6 Stunden 28 Minuten</li><li><span class="label">Format:</span> MP3 - 192kb/s (Multitrack/Onetrack)</li><li><span class="label">Tracks:</span> 64</li><li><span class="label">Erscheinungsdatum:</span> 14.12.2018</li><li><span class="label">Copyright:</span> Eins A Medien GmbH, Köln;© Pabel-Moewig Verlag KG, Rastatt</li></ul></div>
                </div>

"""

//type ProductSite = HtmlProvider< productSiteHtml >

let ps () = 
    
    let paragraphs =
        HtmlDocument
            .Parse(productSiteHtml)                        
            .Descendants ["p"]
        |> Seq.toList
    
    let productDetail = 
        paragraphs
        |> List.tryFindIndex (fun i -> 
            let idAttribute = i.TryGetAttribute("class")
            match idAttribute with
            | None -> false
            | Some a ->
                a.Value() = "pricetag"
            )
        |> Option.bind( 
            fun idx ->
                //get next entry
                let nextIdx = idx + 1
                if (nextIdx + 1) > paragraphs.Length then
                    None
                else
                    let nextEntry = paragraphs.[nextIdx]
                    let description = nextEntry.InnerText()
                    Some description
        )    
    
    productDetail
        
#r "netstandard.dll"
#r @"C:\Users\Dieselmeister\.nuget\packages\sixlabors.imagesharp\1.0.0-beta0005\lib\netstandard2.0\SixLabors.ImageSharp.dll"
#r @"C:\Users\Dieselmeister\.nuget\packages\system.memory\4.5.2\lib\netstandard2.0\System.Memory.dll"
#r @"C:\Users\Dieselmeister\.nuget\packages\sixlabors.core\1.0.0-beta0006\lib\netcoreapp2.0\SixLabors.Core.dll"
#r @"C:\Users\Dieselmeister\.nuget\packages\system.buffers\4.5.0\lib\netstandard2.0\System.Buffers.dll"
#r @"C:\Users\Dieselmeister\.nuget\packages\system.runtime.compilerservices.unsafe\4.5.0\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll"
#r @"C:\Users\Dieselmeister\.nuget\packages\System.Numerics.Vectors\4.5.0\lib\netstandard2.0\System.Numerics.Vectors.dll"



            
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open System.IO

let thumb = SixLabors.ImageSharp.Image.Load(@"E:\Downloads\PERRY_RHODAN_3025_Ich_erinnere_mich\PERRY RHODAN 3025 - Ich erinnere mich\PR3025_Ich_erinnere_mich.jpg")


thumb.Mutate(fun x -> 
    x.Resize(200,200) |> ignore
    ()
    ) |> ignore                                        

let fileStream = new FileStream(@"E:\Downloads\PERRY_RHODAN_3025_Ich_erinnere_mich\PERRY RHODAN 3025 - Ich erinnere mich\test.jpg",FileMode.Create)
thumb.SaveAsJpeg(fileStream)
fileStream.Close()
    
            

