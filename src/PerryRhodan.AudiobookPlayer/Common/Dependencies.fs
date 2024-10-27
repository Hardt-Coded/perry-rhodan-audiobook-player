module Dependencies

open Microsoft.Extensions.DependencyInjection



let internal sc = ServiceCollection()
let mutable internal serviceProvider = Operators.Unchecked.defaultof<ServiceProvider>



type DependencyService() =
    
    static member Get<'a>()=
        if serviceProvider = null then
            DependencyService.SetComplete()
            
        serviceProvider.GetService<'a>()
        
    static member ServiceCollection with get() = sc

    static member SetComplete() =
        serviceProvider <- sc.BuildServiceProvider()
            
    static member IsComplete with get() = serviceProvider <> null