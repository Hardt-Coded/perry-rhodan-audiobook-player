﻿module Dependencies

open Microsoft.Extensions.DependencyInjection



let internal sc = ServiceCollection()
let mutable internal serviceProvider = Operators.Unchecked.defaultof<ServiceProvider>



type DependencyService() =
    
    static member Get<'a>()=
        serviceProvider.GetService<'a>()
        
    static member ServiceCollection with get() = sc

    static member SetComplete() =
        if serviceProvider = null then
            serviceProvider <- sc.BuildServiceProvider()
            
    static member IsComplete with get() = serviceProvider <> null