module Size

[<Measure>]
type byte

[<Measure>]
type kB

[<Measure>]
type MB

let bytesPerKiloByte = 1024L<byte/kB>
let kilobytesPerMegaByte = 1024L<kB/MB>

// Convert int64 to bytes with unit of measure
let int64ToBytes (x: int64) : int64<byte> = 
    x * 1L<byte>

// Convert bytes to kilobytes
let bytesToKiloBytes (x: int64<byte>) : int64<kB> = 
    x / bytesPerKiloByte

// Convert bytes to megabytes
let bytesToMegaBytes (x: int64<byte>) : int64<MB> = 
    x / bytesPerKiloByte / kilobytesPerMegaByte
