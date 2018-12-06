#r "netstandard"
#r "System.Xml.Linq"
#r @"C:\Users\Dieselmeister\.nuget\packages\fsharp.data\3.0.0\lib\netstandard2.0\FSharp.Data.dll"

open FSharp.Data
open System
open System.Text.RegularExpressions

type DownloadSite = HtmlProvider<"./einsamediaDownloadPage.txt">

let tst = 
    DownloadSite.Load("./einsamediaDownloadPage.txt").Html.Descendants("div")
    |> Seq.toArray 
    |> Array.filter (fun i -> i.AttributeValue("id") = "downloads")
    |> Array.tryHead
    |> Option.map (fun i -> i.Descendants("li") |> Seq.toArray)
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

                (episodeNumber,episodeTitle,linkForMultiDownload,linkProductSite, i)
            )
        |> Seq.sortBy (fun (epNumber,_,_,_,_) -> 
                match epNumber with
                | None -> -1
                | Some x -> x
            )
    )     
|> Seq.iter ( fun (key,items) -> 
        printfn "---- %s ----" key
        items |> Seq.iter (fun (epNumber,epTitel,link,plink,i) -> printfn "%A - %s (%A) (%A)" epNumber epTitel link plink)
    )



