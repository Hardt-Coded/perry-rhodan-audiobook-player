namespace PerryRhodan.AudiobookPlayer

open System
open Avalonia.Controls
open Avalonia.Controls.Templates
open PerryRhodan.AudiobookPlayer.ViewModels
open ReactiveElmish.Avalonia

(*
type ViewLocator() =
    interface IDataTemplate with
        
        member this.Build(data) =
            if isNull data then
                null
            else    
                let name = data.GetType().FullName.Replace("ViewModel", "View", StringComparison.Ordinal)
                let typ = Type.GetType(name)
                if isNull typ then
                    upcast TextBlock(Text = sprintf "Not Found: %s" name)
                else
                    downcast Activator.CreateInstance(typ)

        member this.Match(data) = data :? ViewModelBase
        *)

type ViewLocator() =
    interface IDataTemplate with
        
        member this.Build(data) =
            let t = data.GetType()
            let viewName = t.FullName.Replace("ViewModels", "Views").Replace("ViewModel", "View")
            let parts = viewName.Split([|'['; '+'|], StringSplitOptions.RemoveEmptyEntries)
            let name = 
                if parts.Length > 2 
                then parts[1]
                else parts[0]
            let viewType = Type.GetType(name)
            if isNull viewType then
                TextBlock(Text = sprintf "Not Found: %s" name)
            else
                let view = downcast Activator.CreateInstance(viewType)
                match data with 
                | :? ReactiveUI.ReactiveObject as vm ->
                    ViewBinder.bindWithDisposeOnViewUnload (vm, view) |> snd
                | _ ->
                    TextBlock(Text = sprintf $"Not found: %s{name}")
                
        member this.Match(data) = 
            // Only apply this IDataTemplate when data is an IElmishViewModel
            data :? ReactiveUI.ReactiveObject