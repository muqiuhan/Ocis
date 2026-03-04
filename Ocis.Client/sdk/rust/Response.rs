#![allow(dead_code,)]
#![allow(non_camel_case_types,)]
#![allow(non_snake_case,)]
#![allow(non_upper_case_globals,)]
#![allow(unreachable_code,)]
#![allow(unused_attributes,)]
#![allow(unused_imports,)]
#![allow(unused_macros,)]
#![allow(unused_parens,)]
#![allow(unused_variables,)]
#![allow(unused_assignments,)]
mod module_8ab04c88 {
    pub mod Ocis {
        use super::*;
        pub mod Client {
            use super::*;
            pub mod SDK {
                use super::*;
                pub mod Response {
                    use super::*;
                    use fable_library_rust::Native_::LrcPtr;
                    use fable_library_rust::NativeArray_::Array;
                    use fable_library_rust::Option_::defaultValue;
                    use fable_library_rust::String_::string;
                    use crate::Ocis::Client::SDK::Protocol::DeserializeResponse;
                    use crate::Ocis::Client::SDK::Protocol::ParseResult_1;
                    use crate::Ocis::Server::ProtocolSpec::ResponsePacket;
                    #[derive(Clone, Hash, PartialEq, PartialOrd,)]
                    pub enum ClientResult_1<T: Clone + 'static> {
                        Success(T),
                        NotFound,
                        Error(string),
                    }
                    impl <T: Clone + 'static> core::fmt::Debug for
                     ClientResult_1<T> {
                        fn fmt(&self, f: &mut core::fmt::Formatter)
                         -> core::fmt::Result {
                            write!(f, "{}", core::any::type_name::<Self>())
                        }
                    }
                    impl <T: Clone + 'static> core::fmt::Display for
                     ClientResult_1<T> {
                        fn fmt(&self, f: &mut core::fmt::Formatter)
                         -> core::fmt::Result {
                            write!(f, "{}", core::any::type_name::<Self>())
                        }
                    }
                    pub fn parseResponse(bytes: Array<u8>)
                     -> LrcPtr<ParseResult_1<LrcPtr<ResponsePacket>>> {
                        DeserializeResponse(bytes)
                    }
                    pub fn toClientResult(parseResult:
                                              LrcPtr<ParseResult_1<LrcPtr<ResponsePacket>>>)
                     ->
                         LrcPtr<Ocis::Client::SDK::Response::ClientResult_1<()>> {
                        match parseResult.as_ref() {
                            ParseResult_1::ParseError(parseResult_1_0) =>
                            LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Error::<()>(parseResult_1_0.clone())),
                            ParseResult_1::InsufficientData =>
                            LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Error::<()>(string("Insufficient data"))),
                            ParseResult_1::ParseSuccess(parseResult_0_0) => {
                                let response: LrcPtr<ResponsePacket> =
                                    parseResult_0_0.clone();
                                let matchValue: u8 = response.StatusCode;
                                match &matchValue {
                                    0_u8 =>
                                    LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Success::<()>(())),
                                    1_u8 =>
                                    LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::NotFound::<()>),
                                    2_u8 =>
                                    LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Error::<()>(defaultValue(string("Unknown error"),
                                                                                                                      response.ErrorMessage.clone()))),
                                    _ =>
                                    LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Error::<()>(string("Invalid status code"))),
                                }
                            }
                        }
                    }
                    pub fn toClientResultValue(parseResult:
                                                   LrcPtr<ParseResult_1<LrcPtr<ResponsePacket>>>)
                     ->
                         LrcPtr<Ocis::Client::SDK::Response::ClientResult_1<Array<u8>>> {
                        match parseResult.as_ref() {
                            ParseResult_1::ParseError(parseResult_1_0) =>
                            LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Error::<Array<u8>>(parseResult_1_0.clone())),
                            ParseResult_1::InsufficientData =>
                            LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Error::<Array<u8>>(string("Insufficient data"))),
                            ParseResult_1::ParseSuccess(parseResult_0_0) => {
                                let response: LrcPtr<ResponsePacket> =
                                    parseResult_0_0.clone();
                                let matchValue: u8 = response.StatusCode;
                                match &matchValue {
                                    0_u8 => {
                                        let matchValue_1: Option<Array<u8>> =
                                            response.Value.clone();
                                        match &matchValue_1 {
                                            None =>
                                            LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Error::<Array<u8>>(string("Success response missing value"))),
                                            Some(matchValue_1_0_0) =>
                                            LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Success::<Array<u8>>(matchValue_1_0_0.clone())),
                                        }
                                    }
                                    1_u8 =>
                                    LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::NotFound::<Array<u8>>),
                                    2_u8 =>
                                    LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Error::<Array<u8>>(defaultValue(string("Unknown error"),
                                                                                                                             response.ErrorMessage.clone()))),
                                    _ =>
                                    LrcPtr::new(Ocis::Client::SDK::Response::ClientResult_1::Error::<Array<u8>>(string("Invalid status code"))),
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
pub use module_8ab04c88::*;
#[path = "./Binary.rs"]
mod module_d27ee164;
pub use module_d27ee164::*;
#[path = "./Protocol.rs"]
mod module_319b107d;
pub use module_319b107d::*;
#[path = "./Request.rs"]
mod module_84d28afa;
pub use module_84d28afa::*;
#[path = "./Ocis.Server/ProtocolSpec.rs"]
mod module_e7d8300b;
pub use module_e7d8300b::*;
pub mod Ocis {
    pub use crate::module_d27ee164::Ocis::*;
    pub use crate::module_319b107d::Ocis::*;
    pub use crate::module_84d28afa::Ocis::*;
    pub use crate::module_8ab04c88::Ocis::*;
    pub use crate::module_e7d8300b::Ocis::*;
    pub mod Client {
        pub use crate::module_d27ee164::Ocis::Client::*;
        pub use crate::module_319b107d::Ocis::Client::*;
        pub use crate::module_84d28afa::Ocis::Client::*;
        pub use crate::module_8ab04c88::Ocis::Client::*;
        pub mod SDK {
            pub use crate::module_d27ee164::Ocis::Client::SDK::*;
            pub use crate::module_319b107d::Ocis::Client::SDK::*;
            pub use crate::module_84d28afa::Ocis::Client::SDK::*;
            pub use crate::module_8ab04c88::Ocis::Client::SDK::*;
        }
    }
    pub mod Server {
        pub use crate::module_e7d8300b::Ocis::Server::*;
    }
}
