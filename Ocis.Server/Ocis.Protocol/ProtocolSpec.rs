pub mod Ocis {
    use super::*;
    pub mod Server {
        use super::*;
        pub mod ProtocolSpec {
            use super::*;
            use fable_library_rust::NativeArray_::Array;
            use fable_library_rust::String_::string;
            /// Request packet
            #[derive(Clone, Debug, Hash, PartialEq, PartialOrd,)]
            pub struct RequestPacket {
                pub MagicNumber: u32,
                pub Version: u8,
                pub CommandType: i32,
                pub TotalPacketLength: i32,
                pub KeyLength: i32,
                pub ValueLength: i32,
                pub Key: Array<u8>,
                pub Value: Option<Array<u8>>,
            }
            impl core::fmt::Display for RequestPacket {
                fn fmt(&self, f: &mut core::fmt::Formatter)
                 -> core::fmt::Result {
                    write!(f, "{}", core::any::type_name::<Self>())
                }
            }
            /// Response packetnse packet
            #[derive(Clone, Debug, Hash, PartialEq, PartialOrd,)]
            pub struct ResponsePacket {
                pub MagicNumber: u32,
                pub Version: u8,
                pub StatusCode: u8,
                pub TotalPacketLength: i32,
                pub ValueLength: i32,
                pub ErrorMessageLength: i32,
                pub Value: Option<Array<u8>>,
                pub ErrorMessage: Option<string>,
            }
            impl core::fmt::Display for ResponsePacket {
                fn fmt(&self, f: &mut core::fmt::Formatter)
                 -> core::fmt::Result {
                    write!(f, "{}", core::any::type_name::<Self>())
                }
            }
        }
    }
}
