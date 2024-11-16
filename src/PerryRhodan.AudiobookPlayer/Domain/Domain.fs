module Domain

open System
open System.Text.RegularExpressions
open Common
open HtmlAgilityPack


type AudioBookPosition =
    { Filename: string
      Position: TimeSpan }

type AudioBookState =
    { Completed:bool
      CurrentPosition: AudioBookPosition option
      Downloaded:bool
      DownloadedFolder:string option
      LastTimeListend: DateTime option }

    with
        static member Empty = {Completed = false; CurrentPosition = None; Downloaded=false; DownloadedFolder = None; LastTimeListend = None}

type AudioBook = {
        Id:int
        FullName:string
        EpisodeNo:int option
        EpisodenTitel: string
        Group:string
        Picture:string option
        Thumbnail:string option
        DownloadUrl: string option
        ProductSiteUrl:string option
        State: AudioBookState
        AmbientColor: string option
        ShopId:string option
    }

    with
        static member Empty = {
            Id = 0
            ShopId = None
            FullName=""
            EpisodeNo=None
            EpisodenTitel = ""
            Group = ""
            Picture=None
            Thumbnail=None
            DownloadUrl=None
            ProductSiteUrl = None
            State=AudioBookState.Empty
            AmbientColor = None }

type NameGroupedAudioBooks = (string * AudioBook[])[]

type AudioBookListType =
    | GroupList of NameGroupedAudioBooks
    | AudioBookList of string * AudioBook[]




type AudioBookAudioFile = {
    FileName:string
    Duration:TimeSpan
}

type AudioBookAudioFilesInfo = {
    Id: int
    AudioFiles: AudioBookAudioFile list
}



module Helpers =

    let getDownloadNameRegex (innerText:string) =
        let indexFirst = innerText.IndexOf("(")
        let part = innerText[..indexFirst]

        let minusIdx = part.IndexOf(" - ")
        let doubleIdx = part.IndexOf(": ")

        if (minusIdx > doubleIdx) then
            Regex(@"^([A-Za-z .:-]*[0-9.]*[A-Za-z .:-]*)([0-9]*)( - )([\w\säöüÄÖÜ.:!\-\/]*[\(\)Teil \d]*)(.*)")
        elif innerText.IndexOf("Episode") > -1 && innerText.IndexOf("Episode") < innerText.IndexOf(":")  then
            Regex(@"^([A-Za-z .:-]*[0-9.]*[A-Za-z .:-]*)Episode\s*([0-9]*)(: )([\w\säöüÄÖÜ.:!\-\/]*[\(\)Teil \d]*)(.*)")
        else
            Regex(@"^([A-Za-z .-]*)([0-9]*)(:| - | )([\w\säöüÄÖÜ.:!\-\/]*[\(\)Teil \d]*)(.*)")


    let regexReplace searchRegex newText (input:string) =
        let regex = Regex(searchRegex)
        if regex.IsMatch(input) then
            let searchVal = regex.Match(input).Groups[0].Value
            input.Replace(searchVal, newText)
        else
            input


    let getKey (downloadNameRegex:Regex) (innerText:string) =
        if not (downloadNameRegex.IsMatch(innerText)) then "Other"
        else
            let matchTitle = downloadNameRegex.Match(innerText)
            matchTitle.Groups[1].Value.Replace("Nr.", "").Trim()


    let tryGetEpisodenNumber (downloadNameRegex:Regex) innerText =

        let fallBackEpNum () =
            let innerText =
                innerText
                |> regexReplace "40\.000" ""

            let ep1Regex = Regex("\d+")
            if ep1Regex.IsMatch(innerText) then
                let isNum,num = Int32.TryParse(ep1Regex.Match(innerText).Groups[0].Value)
                if isNum then Some num else None
            else
                None

        if downloadNameRegex.IsMatch(innerText) then
            let epNumRes = Int32.TryParse(downloadNameRegex.Match(innerText).Groups[2].Value)
            match epNumRes with
            | true, x -> Some x
            | _ -> fallBackEpNum ()
        else
            fallBackEpNum ()

    open System.Linq

    let getEpisodenTitle (downloadNameRegex:Regex) (innerText:string) =
        if not (downloadNameRegex.IsMatch(innerText)) then innerText.Trim()
        else
            let ept = downloadNameRegex.Match(innerText).Groups[4].Value.Trim()
            ept.Substring(0,(ept.Length-2)).ToString().Trim().Replace(":"," -")
            
            
    let getNewShopEpisodenTitle (downloadNameRegex:Regex) (innerText:string) =
        if not (downloadNameRegex.IsMatch(innerText)) then innerText.Trim()
        else
            let ept = downloadNameRegex.Match(innerText).Groups[4].Value.Trim()
            ept.Trim().Replace(":"," -")


    let tryGetLinkForMultiDownload (i:HtmlNode) =
        i.Descendants("a")
        |> Seq.filter (_.Attributes.FirstOrDefault(fun x -> x.Name = "href").Value.ToLower().Contains("productfiletypeid=2"))
        |> Seq.map (_.Attributes.FirstOrDefault(fun x -> x.Name = "href").Value)
        |> Seq.tryHead


    let tryGetProductionPage (i:HtmlNode) =
        i.Descendants("a")
        |> Seq.filter (fun i -> i.InnerText = "ansehen")
        |> Seq.map (_.Attributes.FirstOrDefault(fun x -> x.Name = "href").Value)
        |> Seq.tryHead


    let buildFullName episodeNumber key episodeTitle =
        match episodeNumber with
        | None -> $"%s{key} - %s{episodeTitle}"
        | Some no -> $"%s{key} %i{no} - %s{episodeTitle}"
        
        
    let buildNewShopFullName episodeNumber key episodeTitle =
        match episodeNumber with
        | None -> $"%s{key} - %s{episodeTitle}"
        | Some no -> $"%s{key} Nr. %i{no} - %s{episodeTitle}"


    let tryParseInt (str:string) =
        let is,v = Int32.TryParse(str)
        if is then Some v else None


    let tryGetProductId linkProductSite =
        linkProductSite
        |> RegExHelper.regexMatchGroupOpt 2 "(productID=)(\d*)"
        |> Option.bind tryParseInt


    let compansateManually (input:string) =
        if input.Contains("Perry Rhodan Storys") then
            input
            |> regexReplace "\([A-Z ]*\d+\)" ""
        else
            input


    type CompansationItem = {
        ProductId:int
        FullName:string
        Group:string
        EpisodeNo: int option
        EpisodeTitle: string
    }

    let targetCompansation id =
        let items = [
            {
                ProductId = 27718
                FullName = "Perry Rhodan Silber Edition 21 - Straße nach Andromeda"
                Group = "Perry Rhodan Silber Edition"
                EpisodeNo = Some 21
                EpisodeTitle = "Straße nach Andromeda"
            }
        ]
        items |> List.tryFind (fun i -> i.ProductId = id)

open System.Linq

let parseOldShopDownloadData htmlData =
    let document = HtmlDocument()
    document.LoadHtml(htmlData)
    let divModes = document.DocumentNode.SelectNodes("//div[@id='downloads']")
    divModes
    |> Seq.toArray
    |> Array.tryHead
    // only the audiobooks
    |> Option.map (
        fun i ->
            i.Descendants("li")
            |> Seq.filter (fun i -> not (i.InnerText.Contains("Impressum")))
            |> Seq.filter (fun i ->
                i.Descendants("a")
                |> Seq.exists (fun i ->
                    i.Attributes
                        .Where(fun x -> x.Name = "href")
                        .Any(fun x-> x.Value.Contains("butler.php?action=audio"))
                    )
            )
            |> Seq.toArray
    )
    |> Option.defaultValue [||]
    |> Array.Parallel.map (
        fun i ->
            let downloadRegex = Helpers.getDownloadNameRegex i.InnerText
            let innerText = i.InnerText |> Helpers.compansateManually
            //   little title work
            let key = innerText |> Helpers.getKey downloadRegex
            let epNum = innerText |> Helpers.tryGetEpisodenNumber downloadRegex
            let episodeNumber =
                if epNum = None then
                    i.InnerText |> Helpers.tryGetEpisodenNumber downloadRegex
                else
                    epNum
            let episodeTitle = innerText |> Helpers.getEpisodenTitle downloadRegex
            let linkForMultiDownload = i |> Helpers.tryGetLinkForMultiDownload
            let linkProductSite = i |> Helpers.tryGetProductionPage
            let fullName = Helpers.buildFullName episodeNumber key episodeTitle
            let productId =
                linkProductSite
                |> Helpers.tryGetProductId
                |> Option.defaultValue -1

            // manual compantation of entries
            let fullName,episodeTitle,episodeNumber,key =
                match Helpers.targetCompansation productId with
                | None ->
                    fullName,episodeTitle,episodeNumber,key
                | Some item ->
                    item.FullName, item.EpisodeTitle, item.EpisodeNo, item.Group


            {   Id = productId
                ShopId = Some (productId.ToString())
                FullName = fullName
                EpisodeNo = episodeNumber
                EpisodenTitel = episodeTitle
                Group = key
                DownloadUrl = linkForMultiDownload
                ProductSiteUrl = linkProductSite
                Picture = None
                Thumbnail = None
                State = AudioBookState.Empty
                AmbientColor = None }
    )
    |> Array.filter (fun i -> i.Id <> -1)
    |> Array.sortBy (fun ab ->
            match ab.EpisodeNo with
            | None -> -1
            | Some x -> x
    )
    |> Array.distinct


let private onlyNumRegex = Regex(@"\d+", RegexOptions.Compiled)


let parseNewShopDownloadData htmlData =
    
    let document = HtmlDocument()
    document.LoadHtml htmlData
    // Todo: null check
    document.DocumentNode.SelectNodes("//div[contains(@id,'product')]")
    |> Seq.toList
    |> List.map (fun node ->
        let title = node.SelectSingleNode(".//span[contains(@class,'audiobook-item-title')]").InnerText
        let productUrl = node.SelectSingleNode(".//a").GetAttributeValue("href", "") |> Option.ofObj
        // the download url is the one, where the download attribute contains a zip file
        let downloadUrl = $"""https://www.einsamedien-verlag.de{node.SelectSingleNode(".//a[contains(@download,'.zip')]").GetAttributeValue("href", "")}"""
        let downloadRegex = Helpers.getDownloadNameRegex title
        let innerText = title |> Helpers.compansateManually
        let key = innerText |> Helpers.getKey downloadRegex
        let epNum = innerText |> Helpers.tryGetEpisodenNumber downloadRegex
        let episodeNumber =
            if epNum = None then
                title |> Helpers.tryGetEpisodenNumber downloadRegex
            else
                epNum
        let episodeTitle = innerText |> Helpers.getNewShopEpisodenTitle downloadRegex
        // picture is on a button with the attibute "data-artwork-url"
        let pictureUrl =
            // get the 512x512 picture
            node.SelectSingleNode(".//img").GetAttributeValue("srcset", "")
            |> Option.ofObj
            |> Option.bind (fun x ->
                // split by ,
                let splitted =
                    x.Split(',')
                    |> Array.map (fun x -> x.Trim())
                    |> Array.map (fun x -> x.Split('?').[0])
                    
                match splitted with
                | [||] ->
                    // get url from img tag
                    node.SelectSingleNode(".//img").GetAttributeValue("src", "") |> Option.ofObj
                    
                | _ ->
                    let bestPicture =
                        splitted
                        |> Array.tryFind (fun x -> x.Contains("512x512"))
                        |> Option.defaultValue splitted.[0]
                        
                    Some bestPicture
            )
            
            
        let thumbnailUrl =
            // get the 512x512 picture
            node.SelectSingleNode(".//img").GetAttributeValue("srcset", "")
            |> Option.ofObj
            |> Option.bind (fun x ->
                // split by ,
                let split =
                    x.Split(',')
                    |> Array.map (fun x -> x.Trim())
                    |> Array.map (fun x -> x.Split('?').[0])
                    
                match split with
                | [||] ->
                    // get url from img tag
                    node.SelectSingleNode(".//img").GetAttributeValue("src", "") |> Option.ofObj
                    
                | _ ->
                    // 280x280, or 360x360 or the first one
                    let bestPicture =
                        let firstChoice =
                            split
                            |> Array.tryFind (fun x -> x.Contains("280x280"))
                        match firstChoice with
                        | Some x -> Some x
                        | None ->
                            split
                            |> Array.tryFind (fun x -> x.Contains("360x360"))
                            |> Option.defaultValue split.[0]
                            |> Some
                        
                    bestPicture
            )
            
                
        let internalProductId =
            // product id is in the div id after the word "product"
            node.GetAttributeValue("id", "")
            |> Option.ofObj
            |> Option.bind (fun x ->
                // extract the number use regex
                onlyNumRegex.Match(x) |> Option.ofObj |> Option.map (fun x -> x.Value |> Int32.Parse) 
            )
            |> Option.defaultValue -1
            
        let shopProductId =
            // product id is in the div id after the word "product"
            node.GetAttributeValue("id", "")
            |> Option.ofObj
            |> Option.map (_.Replace("product", ""))
            
            
        let fullName = Helpers.buildNewShopFullName episodeNumber key episodeTitle
        
        {
            Id = internalProductId
            ShopId = shopProductId
            FullName = fullName
            EpisodeNo = episodeNumber
            EpisodenTitel = episodeTitle
            Group = key
            DownloadUrl = Some downloadUrl
            ProductSiteUrl = productUrl
            Picture = pictureUrl
            Thumbnail = thumbnailUrl
            State = AudioBookState.Empty
            AmbientColor = None
        }
    )


let filterNewAudioBooks (local:AudioBook[]) (online:AudioBook[]) =
    online
    |> Array.filter (fun i -> local |> Array.exists (fun l -> l.Id = i.Id) |> not)


let synchronizeAudiobooks (local:AudioBook[]) (online:AudioBook[]) =
    let differences = filterNewAudioBooks local online
    Array.concat [|local; differences|]


let findDifferentAudioBookNames (local:AudioBook[]) (online:AudioBook[]) =
    local
    |> Array.choose (
        fun i ->
            online
            |> Array.tryFind (fun l -> l.Id = i.Id)
            |> Option.bind (fun n ->
                if n.FullName = i.FullName then None else Some n
            )
    )

//type ProductSite = HtmlProvider< SampleData.productSiteHtml >

let parseProductPageForDescription html =
    let document = HtmlDocument()
    document.LoadHtml(html)

    let paragraphs =
        document.DocumentNode
            .Descendants("p")
        |> Seq.toList

    let productDetail =
        paragraphs
        |> List.tryFindIndex (fun i ->
            let idAttribute = i.Attributes.FirstOrDefault(fun x -> x.Name = "id") |> Option.ofObj
            match idAttribute with
            | None -> false
            | Some a ->
                a.Value = "pricetag"
            )
        |> Option.bind(
            fun idx ->
                //get next entry
                let nextIdx = idx + 1
                if (nextIdx + 1) > paragraphs.Length then
                    None
                else
                    let nextEntry = paragraphs[nextIdx]
                    let description = nextEntry.InnerText
                    Some description
        )


    productDetail

let parseProductPageForImage html =
    let document = HtmlDocument()
    document.LoadHtml(html)

    let image =
        document.DocumentNode
            .Descendants("img")
        |> Seq.tryFind (fun i -> i.Attributes.FirstOrDefault(fun x -> x.Name = "id").Value = "imgzoom")
        |> Option.bind (
            fun i ->
                let imgSrc = i.Attributes.FirstOrDefault(fun x -> x.Name = "src").Value
                if imgSrc = "" then None else Some imgSrc
        )

    image





module Filters =

    let nameFilter audiobooks =
        audiobooks
        |> Array.groupBy (_.Group)
        |> Array.sortBy (fun (key,_) -> key)


    let nameGroupFilter groupname audiobooks =
        match audiobooks with
        | AudioBookList (key,x) -> x
        | GroupList gl ->
            gl
            |> Array.filter (fun (key,_) -> key = groupname)
            |> Array.Parallel.collect (fun (_,items) -> items)


    let mapFromToName items =
        let sortedItem =
            items
            |> Array.sortBy (
                fun i ->
                    i.EpisodeNo |> Option.defaultValue 0
            )
        let groupName = sortedItem[0].Group
        let firstEpNo = sortedItem[0].EpisodeNo |> Option.defaultValue 0
        let lastEpNo = sortedItem[sortedItem.Length - 1].EpisodeNo |> Option.defaultValue 0
        $"%s{groupName} %i{firstEpNo} - %i{lastEpNo}"

    let ``100 episode filter`` audiobooks =
        audiobooks
        |> Array.groupBy (
            fun i ->
                let epno = i.EpisodeNo |> Option.defaultValue 0
                let rest = epno % 100
                let episodeNo = epno - rest
                episodeNo
        )
        |> Array.map (fun (key,items) -> items |> mapFromToName, items)
        |> GroupList

    let ``10 episode filter`` audiobooks : AudioBookListType =
        audiobooks
        |> Array.groupBy (
            fun i ->
                let epno = i.EpisodeNo |> Option.defaultValue 0
                let rest = epno % 10
                let episodeNo = epno - rest
                episodeNo
        )
        |> Array.map (fun (key,items) -> items |> mapFromToName, items)
        |> GroupList

    let (|EpisodeNoMore100|_|) audiobooks =
        let episodeNoGreater100 =
            audiobooks |> Array.exists (fun i -> (i.EpisodeNo |> Option.defaultValue 0) > 100)

        let episodeNoSame100 =
            match audiobooks with
            | [||] -> true
            | x ->
                let epBase = (x[0].EpisodeNo |> Option.defaultValue 0) / 100
                audiobooks |> Array.forall (fun i -> ((i.EpisodeNo |> Option.defaultValue 0) / 100) = epBase)

        if episodeNoGreater100 && (episodeNoSame100 |> not) then Some() else None

    let (|EpisodeNoMore10|_|) audiobooks =
        let episodeNoGreater10 =
            audiobooks |> Array.exists (fun i -> (i.EpisodeNo |> Option.defaultValue 0) > 10)

        let episodeNoSame10 =
            match audiobooks with
            | [||] -> true
            | x ->
                let epBase = (x[0].EpisodeNo |> Option.defaultValue 0) / 10
                audiobooks |> Array.forall (fun i -> ((i.EpisodeNo |> Option.defaultValue 0) / 10) = epBase)

        if episodeNoGreater10 && (episodeNoSame10 |> not) then Some() else None


    let groupsFilter (groups:string list) (audiobooks:AudioBookListType) =
        (audiobooks,groups)
        ||> List.fold (
            fun state item ->
                let nameFilterResult =
                    state
                    |> nameGroupFilter item


                match nameFilterResult with
                | EpisodeNoMore100 ->
                    (nameFilterResult |> ``100 episode filter``)
                | EpisodeNoMore10 ->
                    (nameFilterResult |> ``10 episode filter``)
                | _ ->
                    AudioBookList (item ,nameFilterResult)
        )



let getAudiobooksFromGroup group (audiobooks:(string*AudioBook[])[]) =
    audiobooks
    |> Array.tryFind (fun (key,_) -> key = group)
    |> Option.map (fun (_,items) ->
        items
        |> Array.chunkBySize 10
        |> Array.mapi (fun idx cItems ->
            let cItems = cItems |> Array.sortByDescending (fun si -> si.EpisodeNo)
            let firstEpisode = cItems[0].EpisodeNo |> Option.defaultValue 0
            let lastEpisode = (cItems |> Array.last).EpisodeNo |> Option.defaultValue 0

            $"%s{group} %i{firstEpisode} - %i{lastEpisode}",cItems
            )
        |> Array.sortByDescending (fun (key,_) -> key)
        )


let getAudiobooksFromChunk chunk (audiobooks:(string*AudioBook[])[]) =
    audiobooks
    |> Array.tryFind (fun (key,_) -> key = chunk)
    |> Option.map (fun (_,items) ->
            items |> Array.sortByDescending (fun si -> si.EpisodeNo)
        )


module AudioBooks =

    let flatten (input:NameGroupedAudioBooks) =
        input |> Array.collect (fun (_,items) -> items)







