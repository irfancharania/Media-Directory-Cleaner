module Size

[<Measure>]
type byte

[<Measure>]
type kB

[<Measure>]
type MB

let bytesPerKiloByte = 1024L<byte/kB>
let kilobytesPerMegaByte = 1024L<kB/MB>
let bytesToKiloBytes (x : int64<byte>) = x / bytesPerKiloByte
let bytesToMegaBytes (x : int64<byte>) = x / bytesPerKiloByte / kilobytesPerMegaByte
let int64ToBytes x = x * 1L<byte>
