module Cached.Test

open System

open NUnit.Framework

[<Test>]
let ``cachedHere returns previously cached value`` () =
    let random = Random()
    let c = computation "random" {
        let! number = cachedHere { random.Next() }
        return number
    }

    let s = createEmptyStorage ()
    let n1 = runWithStorage s c
    let n2 = runWithStorage s c
    let n3 = runWithStorage s c

    Assert.That(n1, Is.EqualTo(n2))
    Assert.That(n2, Is.EqualTo(n3))

[<Test>]
let ``cachedHere can be used outside of computation expression`` () =
    let cachedInstance = cachedHere { obj() }

    let c = computation "two different instances" {
        let! x = cachedInstance
        let! y = cachedInstance
        return x, y
    }

    let s = createEmptyStorage ()
    let a, b = runWithStorage s c
    Assert.That(a, Is.Not.SameAs(b))

    let a2, b2 = runWithStorage s c
    Assert.That(a, Is.SameAs(a2))
    Assert.That(b, Is.SameAs(b2))

[<Test>]
let ``ReturnFrom is evaluated in correct scope`` () =
    let counter = computation "counter" {
        let! n = cachedHere { ref 0 }
        n.Value <- n.Value + 1
        return n
    }
    // We can't use `counter` computation twice directly.
    // Because that would result in `"twoCounters"` / `"counter"` scope being opened twice.
    let c = computation "twoCounters" {
        let! m = computation 1 { return! counter }
        let! n = computation 2 { return! counter }
        return m.Value, n.Value
    }

    let s = createEmptyStorage ()
    let a, b = runWithStorage s c
    Assert.That(a, Is.EqualTo(1))
    Assert.That(b, Is.EqualTo(1))

    let a, b = runWithStorage s c
    Assert.That(a, Is.EqualTo(2))
    Assert.That(b, Is.EqualTo(2))
