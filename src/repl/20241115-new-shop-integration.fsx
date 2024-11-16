#r "nuget: HtmlAgilityPack, 1.11.33"
#r @"..\PerryRhodan.AudiobookPlayer/bin/Debug/net8.0/PerryRhodan.AudiobookPlayer.dll"

open System
open System.IO
open HtmlAgilityPack
open Domain


let html = File.ReadAllText(@"repl\example.html")

(*

<div id="productSW10020">
            <div class="row g-0">
                <div class="col-auto audiobook-item-img-container">
                    <a tabindex="-1" href="https://www.einsamedien-verlag.de/perry-rhodan-neo-334-die-zwei-monde-download/SW10020"
                           class="audiobook-item-img-link"
                           title="PERRY RHODAN Neo 334: Die zwei Monde (Download)"
                           data-ajax-modal="modal"
                           data-modal-class="quickview-modal"
                           data-url="/quickview/0191c247ea5572698ea59ab877cb996e"
                        >
                                                                                
                        
                        
    
    
    
        
                
        
                
                    
            <img src="https://www.einsamedien-verlag.de/media/b6/d6/1f/1730626290/PRNeo_334_Die_zwei_Monde.jpg?ts=1730626290"                             srcset="https://www.einsamedien-verlag.de/thumbnail/b6/d6/1f/1730626290/PRNeo_334_Die_zwei_Monde_512x512.jpg?ts=1730626291 512w, https://www.einsamedien-verlag.de/thumbnail/b6/d6/1f/1730626290/PRNeo_334_Die_zwei_Monde_280x280.jpg?ts=1730626291 280w, https://www.einsamedien-verlag.de/thumbnail/b6/d6/1f/1730626290/PRNeo_334_Die_zwei_Monde_360x360.jpg?ts=1730626291 360w, https://www.einsamedien-verlag.de/thumbnail/b6/d6/1f/1730626290/PRNeo_334_Die_zwei_Monde_1920x1920.jpg?ts=1730626291 1920w, https://www.einsamedien-verlag.de/thumbnail/b6/d6/1f/1730626290/PRNeo_334_Die_zwei_Monde_800x800.jpg?ts=1730626291 800w, https://www.einsamedien-verlag.de/thumbnail/b6/d6/1f/1730626290/PRNeo_334_Die_zwei_Monde_400x400.jpg?ts=1730626291 400w"                                 sizes="350px"
                                         class="audiobook-item-img" loading="lazy"        />
                                                        </a>
                </div>
                <div class="col">
                    <div class="audiobook-item-info">
                        <div class="row g-0 audiobook-item-row">
                            <div class="col">
                                <div class="row g-0 audiobook-item-right-row">
                                    <div class="audiobook-item-right-col col-lg-7 col-md-12 col-12">
                                                                                <a href="https://www.einsamedien-verlag.de/perry-rhodan-neo-334-die-zwei-monde-download/SW10020"
                                           class="audiobook-item-label"
                                           title="PERRY RHODAN Neo 334: Die zwei Monde (Download)"
                                           data-ajax-modal="modal"
                                           data-modal-class="quickview-modal"
                                           data-url="/quickview/0191c247ea5572698ea59ab877cb996e"
                                        >                                            <span class="audiobook-item-title">PERRY RHODAN Neo 334: Die zwei Monde</span>
                                            <span class="audiobook-item-description">
                                                <span>Länge:</span> 6 Stunden 36 Minuten
                                                &#8226; <span>Veröffentlicht:</span> 05.07.24
                                                                                            </span>
                                        </a>
                                    </div>

                                    <div class="col-lg-5 col-md-12 col-sm-12 col-12">
                                                                                    <div class="order-detail-content-list">
                                                                                                                                                            <div class="row g-0 order-download-row justify-content-end is-download">
                                                            <div class="col-auto download-item">
                                                                <a href="/account/download/0191c247ea5572698ea59ab877cb996e/0192aeff1535704a827066b7a0ae585e"
                                                                   target="_blank" download="PERRY_RHODAN_NEO_334_Die_zwei_Monde.zip"
                                                                   class="btn btn-light btn-sm">
                                                                    <i class="fas fa-download"></i>
                                                                    Multi-Track                                                                </a>
                                                            </div>
                                                        </div>
                                                                                                                                                                                                                                                                    <div class="row g-0 order-download-row justify-content-end is-download">
                                                            <div class="col-auto download-item">
                                                                <a href="/account/download/0191c247ea5572698ea59ab877cb996e/0192e1c2d4ef7e57983cd07351966fbe"
                                                                   target="_blank" download="PRNEO_334_Die_zwei_Monde.mp3"
                                                                   class="btn btn-light btn-sm">
                                                                    <i class="fas fa-download"></i>
                                                                    One-Track                                                                </a>
                                                            </div>
                                                        </div>
                                                                                                                                                                <div class="row g-0 order-download-row justify-content-end">
                                                            <div class="col-auto download-item">
                                                                <button class="btn btn-light btn-sm" data-action="open-player"
                                                                        data-audio-url="/account/stream/0191c247ea5572698ea59ab877cb996e/0192e1c2d4ef7e57983cd07351966fbe"
                                                                        data-episode-series="PERRY RHODAN Neo Paket 39: Primat (Download)"
                                                                        data-episode-nr="PERRY RHODAN Neo 334: Die zwei Monde (Download)"
                                                                        data-product-nr="SW10020"
                                                                        data-narrator="Axel Gottschick"
                                                                        data-artwork-url="https://www.einsamedien-verlag.de/media/b6/d6/1f/1730626290/PRNeo_334_Die_zwei_Monde.jpg?ts=1730626290"
                                                                        data-placeholder-url="https://www.einsamedien-verlag.de/media/b6/d6/1f/1730626290/PRNeo_334_Die_zwei_Monde.jpg?ts=1730626290">
                                                                    <i class="fas fa-play"></i>
                                                                    Abspielen
                                                                </button>
                                                            </div>
                                                        </div>
                                                                                                                                                </div>
                                                                            </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>


*)



let result =
    
    let onlyNumRegex = System.Text.RegularExpressions.Regex(@"\d+", System.Text.RegularExpressions.RegexOptions.Compiled)
    
    
    let document = HtmlDocument()
    document.LoadHtml html
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
                let splitted =
                    x.Split(',')
                    |> Array.map (fun x -> x.Trim())
                    |> Array.map (fun x -> x.Split('?').[0])
                    
                match splitted with
                | [||] ->
                    // get url from img tag
                    node.SelectSingleNode(".//img").GetAttributeValue("src", "") |> Option.ofObj
                    
                | _ ->
                    // 280x280, or 360x360 or the first one
                    let bestPicture =
                        let firstChoice =
                            splitted
                            |> Array.tryFind (fun x -> x.Contains("280x280"))
                        match firstChoice with
                        | Some x -> Some x
                        | None ->
                            splitted
                            |> Array.tryFind (fun x -> x.Contains("360x360"))
                            |> Option.defaultValue splitted.[0]
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
    
result |> Seq.iter (fun x -> printfn $"""%-10s{x.Id.ToString()} |%-40s{x.Group} | %-80s{x.FullName} | %-40s{x.EpisodenTitel} | %-10s{match x.EpisodeNo with | Some y -> y.ToString() | None -> "None"} """)
result |> Seq.iter (fun x -> printfn $"""%-10s{x.Id.ToString()} | %-140s{match x.Thumbnail with | Some y -> y.ToString() | None -> "None"} | %-140s{match x.Picture with | Some y -> y.ToString() | None -> "None"} """) 
    
    
    
        