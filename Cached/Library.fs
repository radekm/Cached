module Cached

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

type Traced<'t> = { // Tracks whether the value was used by a computation.
                    // Unused values are removed by `gcStorage`.
                    mutable Used : bool
                    Value : 't
                  }

type Scope = obj

[<Struct>]
type Addr = { Line : int; Key : obj }

/// Stores values between runs of a computation.
///
/// Values used by a run are available for the next run.
/// Values not used by a run are removed from storage.
type Storage = { Values : Dictionary<Addr, Traced<obj>>
                 Scopes : Dictionary<Scope, Traced<Storage>>
               }

let createEmptyStorage () = { Values = Dictionary()
                              Scopes = Dictionary()
                            }

type Context = { Storage : Storage
                 Line : int }

type Computation<'a> = Context -> 'a

type computation<'s when 's : equality>(scope : 's) =
    // The line number of the last `Bind` occurrence is stored in a context.
    // This line number is then used by functions `cachedHere` and `cachedHereUnder`
    // as a key into the cache.
    //
    // It would be a mistake to use a line number where `cachedHere` or `cachedHereUnder` occurred
    // because their occurrences don't correspond to their evaluations.
    // For example `cachedHere` may occur once outside of computation expression
    //
    //     ```
    //     let random = Random()
    //     let randomNumber = cachedHere { random.Next() }
    //     ```
    //
    // but this occurrence may be used for multiple evaluations inside a single computation expression
    //
    //     ```
    //     let foo = computation "foo" {
    //         let! first = randomNumber
    //         let! second = randomNumber
    //         printfn "Got numbers %d and %d" first second
    //     }
    //     ```
    // If in the example above `cachedHere` used a line number where `cachedHere`
    // occurred as a key into the cache then the example would fail because
    // the same cache key would be used multiple times.
    //
    // Unfortunately using line number of the last occurence of `Bind`
    // doesn't solve this problem in a for loop.
    member _.Bind
        ( ca : Computation<'a>,
          f : 'a -> Computation<'b>,
          [<CallerLineNumber; Optional; DefaultParameterValue(0)>] line : int
        ) : Computation<'b> =

        fun ctx ->
            let a = ca { ctx with Line = line }
            f a ctx

    member _.Return(a : 'a) : Computation<'a> = fun _ctx -> a

    member _.Delay(f : unit -> Computation<'a>) : unit -> Computation<'a> = f

    // Runs computation inside scope `scope`.
    member _.Run(f : unit -> Computation<'a>) : Computation<'a> =
        fun ctx ->
            let found, s = ctx.Storage.Scopes.TryGetValue scope
            let storage =
                if found then
                    if s.Used then failwith $"Scope cannot be used twice: %A{scope}"
                    else
                        s.Used <- true
                        s.Value
                else
                    let storage = createEmptyStorage ()
                    ctx.Storage.Scopes[scope] <- { Used = true; Value = storage }
                    storage
            f () { Storage = storage; Line = -1 }

    // CONSIDER: Shall for loop body be evaluated in a new nested scope?
    //           If so shall a nested scope be named as an item of type `'a` or by an index?
    //           If it shall be named as an item then `'a` must be satisfy equality constraint.

    // It seems that to support `for` loops we have to add `For`, `Zero` and `Combine`.
    member _.For(items : seq<'a>, f : 'a -> Computation<unit>) : Computation<unit> =
        fun ctx ->
            for a in items do
                f a ctx
    member _.Zero() : Computation<unit> = fun _ctx -> ()
    member _.Combine(ca : Computation<unit>, cb : unit -> Computation<'b>) : Computation<'b> =
        fun ctx ->
            ca ctx
            cb () ctx

    member _.Using(resource : 'r when 'r :> IDisposable, f : 'r -> Computation<'a>) : Computation<'a> =
        // NOTE: The following implementation is wrong:
        //
        //     ```
        //     try f resource
        //     finally resource.Dispose()
        //     ```
        //
        // The reason is that we first dispose `resource` and then return partial application `f resource`.
        // Instead we must first evaluate `f resource ctx` and then dispose `resource`.
        fun ctx ->
            try f resource ctx
            finally resource.Dispose()

    member _.ReturnFrom(ca : Computation<'a>) : Computation<'a> = ca

let inline getOrAddCachedValue (storage : Storage) (line : int) (key : 'k) ([<InlineIfLambda>] f : unit -> 'a) =
    let addr = { Line = line; Key = key :> obj }
    let found, v = storage.Values.TryGetValue addr
    let value =
        if found then
            if v.Used then failwith $"Value at %A{addr} cannot be used twice"
            else
                v.Used <- true
                v.Value :?> _  // We have to downcast value because when storing it we converted it to `obj`.
        else
            let value = f ()
            storage.Values[addr] <- { Used = true; Value = value :> obj }
            value
    value

[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
type UserShallNotSeeThis = UserShallNotSeeThis

// `cachedHere { expr }` is translated into `Combine(Yield(expr), Zero())`
// Presence of `Delay` ensures that `expr` in `cachedHere { expr }` is not evaluated too soon.
// `Run` returns a `Computation` so builder `cachedHereUnder` must be used inside computations.
// `Run` also reads value from storage based on the line of the last `Bind` in the computation.
//
// The alternative to this computation expression builder is to simply delay an expression
// with the help of a lambda: `cachedHere <| fun () -> expr`. But `cachedHere { expr }` looks nicer.
type cachedHereUnder<'k when 'k : equality>(key : 'k) =
    member _.Yield( a : 'a ) : 'a = a
    member _.Zero() = UserShallNotSeeThis
    member _.Combine(a : 'a, b : UserShallNotSeeThis) = a
    member _.Delay(f : unit -> 'a) : unit -> 'a = f
    member _.Run(f : unit -> 'a) : Computation<'a> =
        fun ctx ->
            getOrAddCachedValue ctx.Storage ctx.Line key f

// TODO: Investigate how hard it is to have a single occurrence of `cachedHere` on a single line
//       and use it with two different and incompatible types.
//       Eg. we can probably parametrize `computation` by type and then run the same computation
//       but with different type. Does this case happen naturally in practice?

/// Either retrieves value cached at the current line
/// or evaluates delayed expression to create a new value
/// which is then saved to cache.
let cachedHere = cachedHereUnder(())
/// Either retrieves value cached at the current line under `key`
/// or evaluates delayed expression to create a new value
/// which is then saved to cache.
let cachedHereUnder key = cachedHereUnder key

// CONSIDER: Add a variant of `dangerousCachedUnder` which allows using already used values.
type dangerousCachedUnder<'k when 'k : equality>(key : 'k) =
    member _.Yield( a : 'a ) : 'a = a
    member _.Zero() = UserShallNotSeeThis
    member _.Combine(a : 'a, b : UserShallNotSeeThis) = a
    member _.Delay(f : unit -> 'a) : unit -> 'a = f
    member _.Run(f : unit -> 'a) : Computation<'a> =
        fun ctx ->
            getOrAddCachedValue ctx.Storage -1 key f

/// Either retrieves value cached under `key`
/// or evaluates delayed expression to create a new value
/// which is then saved to cache.
///
/// As opposed to `cachedHere*` functions this function doesn't use current line.
/// This makes this function more dangerous because same `key` can
/// be used by two calls at different lines which return different and incompatible types.
let dangerousCachedUnder key = dangerousCachedUnder key

/// From the given storage removes values and scopes which are not marked as used.
let rec private gcStorage (s : Storage) =
    for kv in s.Values do
        if not kv.Value.Used then
            s.Values.Remove kv.Key |> ignore
        else
            kv.Value.Used <- false

    for kv in s.Scopes do
        if not kv.Value.Used then
            s.Scopes.Remove kv.Key |> ignore
        else
            kv.Value.Used <- false
            gcStorage kv.Value.Value

/// `runWithStorage s c` runs computation `c` with a provided storage `s`.
let runWithStorage (s : Storage) (c : Computation<'a>) =
    let a = c { Storage = s; Line = -1 }
    gcStorage s
    a
