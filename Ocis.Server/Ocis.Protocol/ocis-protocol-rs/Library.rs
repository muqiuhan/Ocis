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
            use crate::Ocis::Server::ProtocolSpec::RequestPacket;
            use fable_library_rust::System::Collections::Generic::IEnumerable_1;
            use fable_library_rust::System::Exception;
            /// Parse request packet header from byte array
            pub fn TryParseRequestHeader(buffer: Array<u8>)
             -> Option<LrcPtr<RequestPacket>> {
                if (get_Count(buffer.clone())) < 18_i32 {
                    None::<LrcPtr<RequestPacket>>
                } else {
                    try_catch(||
                                  {
                                      let offset: MutCell<i32> =
                                          MutCell::new(0_i32);
                                      let magicNumber: u32 =
                                          toUInt32(buffer.clone(),
                                                   offset.get().clone());
                                      offset.set((offset.get().clone()) +
                                                     4_i32);
                                      {
                                          let version: u8 =
                                              buffer[offset.get().clone()].clone();
                                          offset.set((offset.get().clone()) +
                                                         1_i32);
                                          {
                                              let commandType: i32 =
                                                  buffer[offset.get().clone()].clone()
                                                      as i32 as i32;
                                              offset.set((offset.get().clone())
                                                             + 1_i32);
                                              {
                                                  let totalPacketLength: i32 =
                                                      toInt32(buffer.clone(),
                                                              offset.get().clone());
                                                  offset.set((offset.get().clone())
                                                                 + 4_i32);
                                                  {
                                                      let keyLength: i32 =
                                                          toInt32(buffer.clone(),
                                                                  offset.get().clone());
                                                      offset.set((offset.get().clone())
                                                                     + 4_i32);
                                                      if if (magicNumber) ==
                                                                1397310287_u32
                                                            {
                                                             (version) == 1_u8
                                                         } else { false } {
                                                          Some(LrcPtr::new(RequestPacket{MagicNumber:
                                                                                             magicNumber,
                                                                                         Version:
                                                                                             version,
                                                                                         CommandType:
                                                                                             commandType,
                                                                                         TotalPacketLength:
                                                                                             totalPacketLength,
                                                                                         KeyLength:
                                                                                             keyLength,
                                                                                         ValueLength:
                                                                                             toInt32(buffer,
                                                                                                     offset.get().clone()),
                                                                                         Key:
                                                                                             new_empty::<u8>(),
                                                                                         Value:
                                                                                             None::<Array<u8>>,}))
                                                      } else {
                                                          None::<LrcPtr<RequestPacket>>
                                                      }
                                                  }
                                              }
                                          }
                                      }
                                  },
                              |matchValue: LrcPtr<Exception>|
                                  None::<LrcPtr<RequestPacket>>)
                }
            }
            /// Parse complete request packet from byte array
            pub fn TryParseRequestPacket(buffer: Array<u8>)
             -> Option<LrcPtr<RequestPacket>> {
                let matchValue: Option<LrcPtr<RequestPacket>> =
                    Ocis::Protocol::TryParseRequestHeader(buffer.clone());
                match &matchValue {
                    None => None::<LrcPtr<RequestPacket>>,
                    Some(matchValue_0_0) => {
                        let header: LrcPtr<RequestPacket> =
                            matchValue_0_0.clone();
                        if (get_Count(buffer.clone())) >=
                               (header.TotalPacketLength) {
                            try_catch(||
                                          {
                                              let offset: MutCell<i32> =
                                                  MutCell::new(18_i32);
                                              let key: Array<u8> =
                                                  getSubArray(buffer.clone(),
                                                              offset.get().clone(),
                                                              header.KeyLength);
                                              offset.set((offset.get().clone())
                                                             +
                                                             (header.KeyLength));
                                              Some(LrcPtr::new(RequestPacket{MagicNumber:
                                                                                 header.MagicNumber,
                                                                             Version:
                                                                                 header.Version,
                                                                             CommandType:
                                                                                 header.CommandType,
                                                                             TotalPacketLength:
                                                                                 header.TotalPacketLength,
                                                                             KeyLength:
                                                                                 header.KeyLength,
                                                                             ValueLength:
                                                                                 header.ValueLength,
                                                                             Key:
                                                                                 key,
                                                                             Value:
                                                                                 if (header.ValueLength)
                                                                                        >
                                                                                        0_i32
                                                                                    {
                                                                                     Some(getSubArray(buffer.clone(),
                                                                                                      offset.get().clone(),
                                                                                                      header.ValueLength))
                                                                                 } else {
                                                                                     None::<Array<u8>>
                                                                                 },}))
                                          },
                                      |matchValue_1: LrcPtr<Exception>|
                                          None::<LrcPtr<RequestPacket>>)
                        } else { None::<LrcPtr<RequestPacket>> }
                    }
                }
            }
            /// Create request packet
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
            /// Serialize request packet to byte array
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
