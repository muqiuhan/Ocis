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
mod module_57be7934 {
    pub mod Ocis {
        use super::*;
        pub mod Protocol {
            use super::*;
            use fable_library_rust::Array_::concat;
            use fable_library_rust::Array_::getSubArray;
            use fable_library_rust::BitConverter_::getBytesInt32;
            use fable_library_rust::BitConverter_::getBytesUInt32;
            use fable_library_rust::BitConverter_::toInt32;
            use fable_library_rust::BitConverter_::toUInt32;
            use fable_library_rust::Encoding_::get_UTF8;
            use fable_library_rust::Exception_::try_catch;
            use fable_library_rust::Native_::LrcPtr;
            use fable_library_rust::Native_::MutCell;
            use fable_library_rust::NativeArray_::Array;
            use fable_library_rust::NativeArray_::add;
            use fable_library_rust::NativeArray_::get_Count;
            use fable_library_rust::NativeArray_::new_array;
            use fable_library_rust::NativeArray_::new_empty;
            use fable_library_rust::NativeArray_::toArray;
            use fable_library_rust::Seq_::ofArray;
            use fable_library_rust::Seq_::toArray as toArray_1;
            use fable_library_rust::String_::sprintf;
            use fable_library_rust::String_::string;
            use crate::Ocis::Server::ProtocolSpec::RequestPacket;
            use crate::Ocis::Server::ProtocolSpec::ResponsePacket;
            use fable_library_rust::System::Collections::Generic::IEnumerable_1;
            use fable_library_rust::System::Exception;
            /// create request packet
            pub fn CreateRequest(commandType: i32, key: Array<u8>,
                                 value: Option<Array<u8>>)
             -> LrcPtr<RequestPacket> {
                let valueLen: i32 =
                    match &value {
                        None => 0_i32,
                        Some(value_0_0) => get_Count(value_0_0.clone()),
                    };
                LrcPtr::new(RequestPacket{MagicNumber: 1397310287_u32,
                                          Version: 1_u8,
                                          CommandType: commandType,
                                          TotalPacketLength:
                                              (18_i32 +
                                                   (get_Count(key.clone()))) +
                                                  (valueLen),
                                          KeyLength: get_Count(key.clone()),
                                          ValueLength: valueLen,
                                          Key: key,
                                          Value: value.clone(),})
            }
            /// serialize request packet to bytes
            pub fn SerializeRequest(packet: LrcPtr<RequestPacket>)
             -> Array<u8> {
                let parts: Array<Array<u8>> = new_empty::<Array<u8>>();
                add(parts.clone(), getBytesUInt32(packet.MagicNumber));
                add(parts.clone(), new_array(&[packet.Version]));
                add(parts.clone(), new_array(&[packet.CommandType as u8]));
                add(parts.clone(), getBytesInt32(packet.TotalPacketLength));
                add(parts.clone(), getBytesInt32(packet.KeyLength));
                add(parts.clone(), getBytesInt32(packet.ValueLength));
                add(parts.clone(), packet.Key.clone());
                {
                    let matchValue: Option<Array<u8>> = packet.Value.clone();
                    match &matchValue {
                        None => (),
                        Some(matchValue_0_0) =>
                        add(parts.clone(), matchValue_0_0.clone()),
                    }
                }
                concat(toArray_1(ofArray(toArray(parts.clone()))))
            }
            /// protocol parse result
            #[derive(Clone, Hash, PartialEq, PartialOrd,)]
            pub enum ParseResult_1<T: Clone + 'static> {
                ParseSuccess(T),
                ParseError(string),
                InsufficientData,
            }
            impl <T: Clone + 'static> core::fmt::Debug for ParseResult_1<T> {
                fn fmt(&self, f: &mut core::fmt::Formatter)
                 -> core::fmt::Result {
                    write!(f, "{}", core::any::type_name::<Self>())
                }
            }
            impl <T: Clone + 'static> core::fmt::Display for ParseResult_1<T>
             {
                fn fmt(&self, f: &mut core::fmt::Formatter)
                 -> core::fmt::Result {
                    write!(f, "{}", core::any::type_name::<Self>())
                }
            }
            /// deserialize response from bytes
            pub fn DeserializeResponse(buffer: Array<u8>)
             ->
                 LrcPtr<Ocis::Protocol::ParseResult_1<LrcPtr<ResponsePacket>>> {
                try_catch(||
                              if (get_Count(buffer.clone())) < 18_i32 {
                                  LrcPtr::new(Ocis::Protocol::ParseResult_1::InsufficientData::<LrcPtr<ResponsePacket>>)
                              } else {
                                  let offset: MutCell<i32> =
                                      MutCell::new(0_i32);
                                  let magicNumber: u32 =
                                      toUInt32(buffer.clone(),
                                               offset.get().clone());
                                  offset.set((offset.get().clone()) + 4_i32);
                                  {
                                      let version: u8 =
                                          buffer[offset.get().clone()].clone();
                                      offset.set((offset.get().clone()) +
                                                     1_i32);
                                      {
                                          let statusCode: u8 =
                                              buffer[offset.get().clone()].clone()
                                                  as u8;
                                          offset.set((offset.get().clone()) +
                                                         1_i32);
                                          {
                                              let totalPacketLength: i32 =
                                                  toInt32(buffer.clone(),
                                                          offset.get().clone());
                                              offset.set((offset.get().clone())
                                                             + 4_i32);
                                              {
                                                  let valueLength: i32 =
                                                      toInt32(buffer.clone(),
                                                              offset.get().clone());
                                                  offset.set((offset.get().clone())
                                                                 + 4_i32);
                                                  {
                                                      let errorMessageLength:
                                                              i32 =
                                                          toInt32(buffer.clone(),
                                                                  offset.get().clone());
                                                      offset.set((offset.get().clone())
                                                                     + 4_i32);
                                                      if (get_Count(buffer.clone()))
                                                             <
                                                             (totalPacketLength)
                                                         {
                                                          LrcPtr::new(Ocis::Protocol::ParseResult_1::InsufficientData::<LrcPtr<ResponsePacket>>)
                                                      } else {
                                                          if !if (magicNumber)
                                                                     ==
                                                                     1397310287_u32
                                                                 {
                                                                  (version) ==
                                                                      1_u8
                                                              } else { false }
                                                             {
                                                              LrcPtr::new(Ocis::Protocol::ParseResult_1::ParseError::<LrcPtr<ResponsePacket>>(string("invalid header")))
                                                          } else {
                                                              if if (valueLength)
                                                                        <
                                                                        0_i32
                                                                    {
                                                                     true
                                                                 } else {
                                                                     (errorMessageLength)
                                                                         <
                                                                         0_i32
                                                                 } {
                                                                  LrcPtr::new(Ocis::Protocol::ParseResult_1::ParseError::<LrcPtr<ResponsePacket>>(string("invalid length field")))
                                                              } else {
                                                                  if (totalPacketLength)
                                                                         !=
                                                                         ((18_i32
                                                                               +
                                                                               (valueLength))
                                                                              +
                                                                              (errorMessageLength))
                                                                     {
                                                                      LrcPtr::new(Ocis::Protocol::ParseResult_1::ParseError::<LrcPtr<ResponsePacket>>(string("packet length mismatch")))
                                                                  } else {
                                                                      let value:
                                                                              Option<Array<u8>> =
                                                                          if (valueLength)
                                                                                 >
                                                                                 0_i32
                                                                             {
                                                                              Some(getSubArray(buffer.clone(),
                                                                                               offset.get().clone(),
                                                                                               valueLength))
                                                                          } else {
                                                                              None::<Array<u8>>
                                                                          };
                                                                      offset.set((offset.get().clone())
                                                                                     +
                                                                                     (valueLength));
                                                                      LrcPtr::new(Ocis::Protocol::ParseResult_1::ParseSuccess::<LrcPtr<ResponsePacket>>(LrcPtr::new(ResponsePacket{MagicNumber:
                                                                                                                                                                                       magicNumber,
                                                                                                                                                                                   Version:
                                                                                                                                                                                       version,
                                                                                                                                                                                   StatusCode:
                                                                                                                                                                                       statusCode,
                                                                                                                                                                                   TotalPacketLength:
                                                                                                                                                                                       totalPacketLength,
                                                                                                                                                                                   ValueLength:
                                                                                                                                                                                       valueLength,
                                                                                                                                                                                   ErrorMessageLength:
                                                                                                                                                                                       errorMessageLength,
                                                                                                                                                                                   Value:
                                                                                                                                                                                       value,
                                                                                                                                                                                   ErrorMessage:
                                                                                                                                                                                       if (errorMessageLength)
                                                                                                                                                                                              >
                                                                                                                                                                                              0_i32
                                                                                                                                                                                          {
                                                                                                                                                                                           let errorBytes:
                                                                                                                                                                                                   Array<u8> =
                                                                                                                                                                                               getSubArray(buffer,
                                                                                                                                                                                                           offset.get().clone(),
                                                                                                                                                                                                           errorMessageLength);
                                                                                                                                                                                           Some(get_UTF8().getString(errorBytes))
                                                                                                                                                                                       } else {
                                                                                                                                                                                           None::<string>
                                                                                                                                                                                       },})))
                                                                  }
                                                              }
                                                          }
                                                      }
                                                  }
                                              }
                                          }
                                      }
                                  }
                              },
                          |ex: LrcPtr<Exception>|
                              LrcPtr::new(Ocis::Protocol::ParseResult_1::ParseError::<LrcPtr<ResponsePacket>>({
                                                                                                                  let arg:
                                                                                                                          string =
                                                                                                                      ex.get_Message();
                                                                                                                  sprintf!("error parsing response: {}",
                                                                                                                           arg)
                                                                                                              })))
            }
            pub mod ProtocolHelper {
                use super::*;
                /// convert string to bytes
                pub fn stringToBytes(s: string) -> Array<u8> {
                    get_UTF8().getBytes(s)
                }
                /// convert bytes to string
                pub fn bytesToString(bytes: Array<u8>) -> string {
                    get_UTF8().getString(bytes)
                }
                /// create SET request
                pub fn createSetRequest(key: string, value: string)
                 -> LrcPtr<RequestPacket> {
                    Ocis::Protocol::CreateRequest(1_i32,
                                                  Ocis::Protocol::ProtocolHelper::stringToBytes(key),
                                                  Some(Ocis::Protocol::ProtocolHelper::stringToBytes(value)))
                }
                /// create GET request
                pub fn createGetRequest(key: string)
                 -> LrcPtr<RequestPacket> {
                    Ocis::Protocol::CreateRequest(2_i32,
                                                  Ocis::Protocol::ProtocolHelper::stringToBytes(key),
                                                  None::<Array<u8>>)
                }
                /// create DELETE request
                pub fn createDeleteRequest(key: string)
                 -> LrcPtr<RequestPacket> {
                    Ocis::Protocol::CreateRequest(3_i32,
                                                  Ocis::Protocol::ProtocolHelper::stringToBytes(key),
                                                  None::<Array<u8>>)
                }
            }
        }
    }
}
pub use module_57be7934::*;
#[path = "../ProtocolSpec.rs"]
mod module_98720b36;
pub use module_98720b36::*;
pub mod Ocis {
    pub use crate::module_57be7934::Ocis::*;
    pub use crate::module_98720b36::Ocis::*;
    pub mod Server {
        pub use crate::module_98720b36::Ocis::Server::*;
    }
}
