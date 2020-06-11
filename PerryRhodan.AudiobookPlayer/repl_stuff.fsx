#r "netstandard"
#r "System.Xml.Linq"
//#r @"C:\Users\Dieselmeister\.nuget\packages\fsharp.data\3.0.0\lib\netstandard2.0\FSharp.Data.dll"
#r @"C:\Users\Dieselmeister\.nuget\packages\fsharp.data\3.0.0\lib\netstandard2.0\FSharp.Data.dll"



open FSharp.Data
open System
open System.Text.RegularExpressions


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

let downloadNameRegexOld = Regex(@"^([A-Za-z .-]*([0-9]+[.][0-9]+)?)([0-9]*)(:| - )([\w\säöüÄÖÜ.:!\-\/]*[\(\)Teil \d]*)(.*)(( - Multitrack \/ Onetrack)|( - Multitrack)|( - Onetrack))")

let meh = Regex(@"^([A-Za-z .-]*([0-9]+[.][0-9]+)?)([0-9]*)(:| - )([\w\säöüÄÖÜ.:!\-\/]*[\(\)Teil \d]*)(.*)")
let muh = Regex(@"^([A-Za-z .:-]*[0-9.]*[A-Za-z .:-]*)([0-9]*)( - )([\w\säöüÄÖÜ.:!\-\/]*[\(\)Teil \d]*)(.*)")

let tst1 = """Warhammer 40.000: Eisenhorn 4 - Magos Teil 2/Der Roman (Hörbuch-Download) (ansehen) - Multitrack"""
let tst2 = """The Horus Heresy 01: Der Aufstieg des Horus (Hörbuch-Download)"""
let tst3 = """Perry Rhodan Arkon 05: Der Smiler und der Hund (Download) (<a href="/index.php?id=16&productID=2146871">ansehen</a>) - <a href="butler.php?action=audio&productID=2146871&productFileTypeID=2">Multitrack</a> / <a href="butler.php?action=audio&productID=2146871&productFileTypeID=3">Onetrack"""
let tst4 = """Perry Rhodan Neo Nr. 008: Die Terraner (Download) (<a href="/index.php?id=16&productID=38646">ansehen</a>) - <a href="butler.php?action=audio&productID=38646&productFileTypeID=2">Multitrack</a> / <a href="butler.php?action=audio&productID=38646&productFileTypeID=3">Onetrack"""
let tst5 = """Perry Rhodan Nr. 3021: Eyshus Geschenk (Hörbuch-Download) (<a href="/index.php?id=16&productID=4009442">ansehen</a>) - <a href="butler.php?action=audio&productID=4009442&productFileTypeID=2">Multitrack</a> / <a href="butler.php?action=audio&productID=4009442&productFileTypeID=3">Onetrack"""

muh.Match(tst1).Groups.[4].Value

downloadNameRegexOld.IsMatch(tst1)
downloadNameRegexOld.Match(tst1).Groups.[1].Value
downloadNameRegexOld.Match(tst1).Groups.[2].Value
downloadNameRegexOld.Match(tst1).Groups.[3].Value
downloadNameRegexOld.Match(tst1).Groups.[4].Value
downloadNameRegexOld.Match(tst1).Groups.[5].Value

downloadNameRegexOld.Match(tst2).Groups.[1]
downloadNameRegexOld.Match(tst3).Groups.[1]

meh.Match(tst1).Groups.[3]
meh.Match(tst2).Groups.[3]
meh.Match(tst3).Groups.[3]
meh.Match(tst4).Groups.[3]
meh.Match(tst5).Groups.[3]





let htmlData =
    File.ReadAllText(@"D:\temp\perryRhodanApp\mega.txt")

module RegExHelper =

    open System.Text.RegularExpressions

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


let getDownloadNameRegex (innerText:string) = 
    let indexFirst = innerText.IndexOf("(")
    let part = innerText.[..indexFirst]
    if (part.Contains(" - ") && part.Contains(": ")) then
        Regex(@"^([A-Za-z .:-]*[0-9.]*[A-Za-z .:-]*)([0-9]*)( - )([\w\säöüÄÖÜ.:!\-\/]*[\(\)Teil \d]*)(.*)")
    else
        Regex(@"^([A-Za-z .-]*)([0-9]*)(:| - )([\w\säöüÄÖÜ.:!\-\/]*[\(\)Teil \d]*)(.*)")


let regexReplace searchRegex newText input =
    let regex = Regex(searchRegex)
    if (regex.IsMatch(input)) then
        let searchVal = regex.Match(input).Groups.[0].Value
        input.Replace(searchVal, newText)
    else
        input


let getKey (downloadNameRegex:Regex) innerText =
    if not (downloadNameRegex.IsMatch(innerText)) then "Other"
    else
        let matchTitle = downloadNameRegex.Match(innerText)
        matchTitle.Groups.[1].Value.Replace("Nr.", "").Trim()


let tryGetEpisodenNumber (downloadNameRegex:Regex) innerText =
    
    let fallBackEpNum () =
        let innerText = 
            innerText
            |> regexReplace "40\.000" ""

        let ep1Regex = Regex("\d+")
        if ep1Regex.IsMatch(innerText) then
            let (isNum,num) = Int32.TryParse(ep1Regex.Match(innerText).Groups.[0].Value)
            if isNum then Some num else None
        else
            None

    if (downloadNameRegex.IsMatch(innerText)) then
        let epNumRes = Int32.TryParse(downloadNameRegex.Match(innerText).Groups.[2].Value)
        match epNumRes with
        | true, x -> Some x
        | _ -> fallBackEpNum ()
    else
        fallBackEpNum ()
            


let getEpisodenTitle (downloadNameRegex:Regex) innerText =
    if not (downloadNameRegex.IsMatch(innerText)) then innerText.Trim()
    else 
        let ept = downloadNameRegex.Match(innerText).Groups.[4].Value.Trim()
        ept.Substring(0,(ept.Length-2)).ToString().Trim().Replace(":"," -")


let tryGetLinkForMultiDownload (i:HtmlNode) =
    i.Descendants["a"]
    |> Seq.filter (fun i ->  i.Attribute("href").Value().ToLower().Contains("productfiletypeid=2"))
    |> Seq.map (fun i -> i.Attribute("href").Value())
    |> Seq.tryHead


let tryGetProductionPage (i:HtmlNode) =
    i.Descendants["a"]
    |> Seq.filter (fun i -> i.InnerText() = "ansehen")
    |> Seq.map (fun i -> i.Attribute("href").Value())
    |> Seq.tryHead


let buildFullName episodeNumber key episodeTitle =
    match episodeNumber with
    | None -> sprintf "%s - %s" key episodeTitle
    | Some no -> sprintf "%s %i - %s" key no episodeTitle


let tryParseInt str =
    let (is,v) = Int32.TryParse(str)
    if is then Some v else None

let tryGetProductId linkProductSite =
    linkProductSite
    |> RegExHelper.regexMatchGroupOpt 2 "(productID=)(\d*)"
    |> Option.bind (tryParseInt)




let compansateManually input =
    input
    |> regexReplace "\([A-Z ]*\d+\)" ""
    

let res =    
    HtmlDocument.Parse(htmlData).Descendants("div")
    |> Seq.toArray 
    |> Array.filter (fun i -> i.AttributeValue("id") = "downloads")
    |> Array.tryHead
    // only the audiobooks
    |> Option.map (
        fun i -> 
            i.Descendants("li") 
            |> Seq.filter (fun i -> not (i.InnerText().Contains("Impressum"))) 
            |> Seq.filter (fun i -> 
                i.Descendants("a") 
                |> Seq.exists (fun i -> 
                    i.TryGetAttribute("href") 
                    |> Option.map(fun m -> m.Value().Contains("butler.php?action=audio")) 
                    |> Option.defaultValue false) 
                )
            |> Seq.toArray
    )
    |> Option.defaultValue ([||])
    |> Array.Parallel.map (
        fun i ->
            let innerText = i.InnerText()
            let downloadRegex = getDownloadNameRegex innerText

            let innerText = innerText |> compansateManually 
            
            //   little title work
            let key = innerText |> getKey downloadRegex
            let epNum = innerText |> tryGetEpisodenNumber downloadRegex   
            let episodeNumber =
                if epNum = None then
                    i.InnerText() |> tryGetEpisodenNumber downloadRegex 
                else
                    epNum


            let episodeTitle = innerText |> getEpisodenTitle downloadRegex
            let linkForMultiDownload = i |> tryGetLinkForMultiDownload
            let linkProductSite = i |> tryGetProductionPage
            let fullName = buildFullName episodeNumber key episodeTitle
            let productId = 
                linkProductSite 
                |> tryGetProductId
                |> Option.defaultValue -1
                        
            {|   
                Id = productId
                FullName = fullName 
                EpisodeNo = episodeNumber
                EpisodenTitel = episodeTitle
                Group = key
                DownloadUrl = linkForMultiDownload 
                ProductSiteUrl = linkProductSite
                |}
    )
    |> Array.filter (fun i -> i.Id <> -1)
    |> Array.sortBy (fun ab -> 
            match ab.EpisodeNo with
            | None -> -1
            | Some x -> x
    )
    |> Array.distinct
    |> Array.iter (fun i ->
        printfn "%s - %i - %s - %s" 
            i.Group
            (i.EpisodeNo |> Option.defaultValue -1)
            i.FullName
            i.EpisodenTitel
    
    )




let innerText = """Perry Rhodan Storys (DVJ 6): Die Leben des Blaise O'Donnell (Hörbuch-Download) (<a href="/index.php?id=16&productID=4210107">ansehen</a>) - <a href="butler.php?action=audio&productID=4210107&productFileTypeID=2">Multitrack</a> / <a href="butler.php?action=audio&productID=4210107&productFileTypeID=3">Onetrack  """

let ep1Regex = Regex("\d+")
ep1Regex.IsMatch(innerText)
let (isNum,num) = Int32.TryParse(ep1Regex.Match(innerText).Groups.[0].Value)
    


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
                <div class="amazingaudioplayer-bar"><div class="amazingaudioplayer-playpause" style="display: block;"><div class="amazingaudioplayer-play" style='background-position: left top; width: 24px; height: 24px; background-image: url("https://www.einsamedien.de/audioplayerengine/playpause-24-24-1.png"); background-repeat: no-repeat; display: block; cursor: pointer;'></div><div class="amazingaudioplayer-pause" style='background-position: right top; width: 24px; height: 24px; background-image: url("https://www.einsamedien.de/audioplayerengine/playpause-24-24-1.png"); background-repeat: no-repeat; display: none; cursor: pointer;'></div></div><div class="amazingaudioplayer-bar-title" style="width: 80px; height: auto; text-indent: -91px; overflow: hidden; display: block; white-space: nowrap;"><span class="amazingaudioplayer-bar-title-text">Perry Rhodan Neo Nr. 189: Die Leiden des Androiden (Hörbuch-Download)</span></div><div class="amazingaudioplayer-volume" style="display: block;"><div class="amazingaudioplayer-volume-button" style='background-position: left top; width: 24px; height: 24px; background-image: url("https://www.einsamedien.de/audioplayerengine/volume-24-24-1.png"); background-repeat: no-repeat; display: block; position: relative; cursor: pointer;'></div><div class="amazingaudioplayer-volume-bar" style="padding: 8px; left: 0px; width: 8px; height: 64px; bottom: 100%; display: none; position: absolute; box-sizing: content-box;"><div class="amazingaudioplayer-volume-bar-adjust" style="width: 100%; height: 100%; display: block; position: relative; cursor: pointer;"><div class="amazingaudioplayer-volume-bar-adjust-active" style="left: 0px; width: 100%; height: 100%; bottom: 0px; display: block; position: absolute;"></div></div></div></div><div class="amazingaudioplayer-time">00:00 / 06:05</div><div class="amazingaudioplayer-progress" style="height: 8px; overflow: hidden; display: block; cursor: pointer;"><div class="amazingaudioplayer-progress-loaded" style="left: 0px; top: 0px; width: 100%; height: 100%; display: block; position: absolute;"></div><div class="amazingaudioplayer-progress-played" style="left: 0px; top: 0px; width: 0%; height: 100%; display: block; position: absolute;"></div></div><div class="amazingaudioplayer-bar-buttons-clear"></div></div><div class="amazingaudioplayer-bar-clear"></div><audio preload=Auto><source src="http://download.einsamedien.de.s3.amazonaws.com/storage/3688426/004_PRNEO_189_Die_Leiden_des_Androiden.mp3?AWSAccessKeyId=AKIAIAWFYTGZGQS2YDPA&amp;Expires=1547542722&amp;Signature=MeRDu5r6Myv2ZhcH1DQjTIvUkwo%3D" type="audio/mpeg"></audio></div>
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


