pub mod Ocis {
    use super::*;
    pub mod Client {
        use super::*;
        pub mod SDK {
            use super::*;
            pub mod Binary {
                use super::*;
                use fable_library_rust::Encoding_::get_UTF8;
                use fable_library_rust::NativeArray_::Array;
                use fable_library_rust::String_::string;
                fn getUint32(b0: u8, b1: u8, b2: u8, b3: u8) -> u32 {
                    (((b0 as u32) | ((b1 as u32) << 8_i32)) |
                         ((b2 as u32) << 16_i32)) | ((b3 as u32) << 24_i32)
                }
                fn getInt32(b0: u8, b1: u8, b2: u8, b3: u8) -> i32 {
                    (((b0 as i32) | ((b1 as i32) << 8_i32)) |
                         ((b2 as i32) << 16_i32)) | ((b3 as i32) << 24_i32)
                }
                pub fn readUInt32LittleEndian(buffer: Array<u8>, offset: i32)
                 -> u32 {
                    Ocis::Client::SDK::Binary::getUint32(buffer[offset].clone(),
                                                         buffer[(offset) +
                                                                    1_i32].clone(),
                                                         buffer[(offset) +
                                                                    2_i32].clone(),
                                                         buffer[(offset) +
                                                                    3_i32].clone())
                }
                pub fn readInt32LittleEndian(buffer: Array<u8>, offset: i32)
                 -> i32 {
                    Ocis::Client::SDK::Binary::getInt32(buffer[offset].clone(),
                                                        buffer[(offset) +
                                                                   1_i32].clone(),
                                                        buffer[(offset) +
                                                                   2_i32].clone(),
                                                        buffer[(offset) +
                                                                   3_i32].clone())
                }
                pub fn readByte(buffer: Array<u8>, offset: i32) -> u8 {
                    buffer[offset].clone()
                }
                pub fn writeUInt32LittleEndian(value: u32, buffer: Array<u8>,
                                               offset: i32) {
                    buffer.get_mut()[offset as usize] = value as u8;
                    buffer.get_mut()[((offset) + 1_i32) as usize] =
                        ((value) >> 8_i32) as u8;
                    buffer.get_mut()[((offset) + 2_i32) as usize] =
                        ((value) >> 16_i32) as u8;
                    buffer.get_mut()[((offset) + 3_i32) as usize] =
                        ((value) >> 24_i32) as u8
                }
                pub fn writeInt32LittleEndian(value: i32, buffer: Array<u8>,
                                              offset: i32) {
                    let u: u32 = value as u32;
                    buffer.get_mut()[offset as usize] = u as u8;
                    buffer.get_mut()[((offset) + 1_i32) as usize] =
                        ((u) >> 8_i32) as u8;
                    buffer.get_mut()[((offset) + 2_i32) as usize] =
                        ((u) >> 16_i32) as u8;
                    buffer.get_mut()[((offset) + 3_i32) as usize] =
                        ((u) >> 24_i32) as u8
                }
                pub fn writeByte(value: u8, buffer: Array<u8>, offset: i32) {
                    buffer.get_mut()[offset as usize] = value;
                }
                pub fn stringToBytes(str: string) -> Array<u8> {
                    get_UTF8().getBytes(str)
                }
                pub fn bytesToString(bytes: Array<u8>) -> string {
                    get_UTF8().getString(bytes)
                }
            }
        }
    }
}
