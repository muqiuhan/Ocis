pub mod Ocis {
    use super::*;
    pub mod Client {
        use super::*;
        pub mod SDK {
            use super::*;
            pub mod Request {
                use super::*;
                use fable_library_rust::Encoding_::get_UTF8;
                use fable_library_rust::Native_::Func1;
                use fable_library_rust::Native_::LrcPtr;
                use fable_library_rust::NativeArray_::Array;
                use fable_library_rust::NativeArray_::get_Count;
                use fable_library_rust::Option_::defaultValue;
                use fable_library_rust::Option_::map;
                use fable_library_rust::String_::string;
                use crate::Ocis::Client::SDK::Protocol::SerializeRequest;
                use crate::Ocis::Server::ProtocolSpec::RequestPacket;
                fn createPacket(commandType: i32, key: string,
                                value: Option<Array<u8>>)
                 -> LrcPtr<RequestPacket> {
                    let keyBytes: Array<u8> = get_UTF8().getBytes(key);
                    let keyLen: i32 = get_Count(keyBytes.clone());
                    let valueLen: i32 =
                        defaultValue(0_i32,
                                     map(Func1::new(move |v: Array<u8>|
                                                        get_Count(v)),
                                         value.clone()));
                    LrcPtr::new(RequestPacket{MagicNumber: 1397310287_u32,
                                              Version: 1_u8,
                                              CommandType: commandType,
                                              TotalPacketLength:
                                                  (18_i32 + (keyLen)) +
                                                      (valueLen),
                                              KeyLength: keyLen,
                                              ValueLength: valueLen,
                                              Key: keyBytes,
                                              Value: value,})
                }
                pub fn createSetRequest(key: string, value: Array<u8>)
                 -> Array<u8> {
                    SerializeRequest(Ocis::Client::SDK::Request::createPacket(1_i32,
                                                                              key,
                                                                              Some(value)))
                }
                pub fn createGetRequest(key: string) -> Array<u8> {
                    SerializeRequest(Ocis::Client::SDK::Request::createPacket(2_i32,
                                                                              key,
                                                                              None::<Array<u8>>))
                }
                pub fn createDeleteRequest(key: string) -> Array<u8> {
                    SerializeRequest(Ocis::Client::SDK::Request::createPacket(3_i32,
                                                                              key,
                                                                              None::<Array<u8>>))
                }
            }
        }
    }
}
