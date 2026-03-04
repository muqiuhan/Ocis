pub mod Ocis {
    use super::*;
    pub mod Client {
        use super::*;
        pub mod SDK {
            use super::*;
            pub mod Protocol {
                use super::*;
                use fable_library_rust::Array_::copyTo;
                use fable_library_rust::Array_::getSubArray;
                use fable_library_rust::Encoding_::get_UTF8;
                use fable_library_rust::Exception_::try_catch;
                use fable_library_rust::Native_::Any;
                use fable_library_rust::Native_::Func1;
                use fable_library_rust::Native_::LrcPtr;
                use fable_library_rust::Native_::MutCell;
                use fable_library_rust::Native_::box_;
                use fable_library_rust::Native_::unbox;
                use fable_library_rust::NativeArray_::Array;
                use fable_library_rust::NativeArray_::get_Count;
                use fable_library_rust::NativeArray_::new_empty;
                use fable_library_rust::NativeArray_::new_init;
                use fable_library_rust::Option_::defaultValue;
                use fable_library_rust::Option_::map;
                use fable_library_rust::String_::sprintf;
                use fable_library_rust::String_::string;
                use crate::Ocis::Client::SDK::Binary::readByte;
                use crate::Ocis::Client::SDK::Binary::readInt32LittleEndian;
                use crate::Ocis::Client::SDK::Binary::readUInt32LittleEndian;
                use crate::Ocis::Client::SDK::Binary::writeByte;
                use crate::Ocis::Client::SDK::Binary::writeInt32LittleEndian;
                use crate::Ocis::Client::SDK::Binary::writeUInt32LittleEndian;
                use crate::Ocis::Server::ProtocolSpec::RequestPacket;
                use crate::Ocis::Server::ProtocolSpec::ResponsePacket;
                use fable_library_rust::System::Exception;
                pub fn TryParseRequestHeader(buffer: Array<u8>)
                 -> Option<LrcPtr<RequestPacket>> {
                    if (get_Count(buffer.clone())) < 18_i32 {
                        None::<LrcPtr<RequestPacket>>
                    } else {
                        try_catch(||
                                      {
                                          let magicNumber: u32 =
                                              readUInt32LittleEndian(buffer.clone(),
                                                                     0_i32);
                                          let version: u8 =
                                              readByte(buffer.clone(), 4_i32);
                                          if if (magicNumber) ==
                                                    1397310287_u32 {
                                                 (version) == 1_u8
                                             } else { false } {
                                              Some(LrcPtr::new(RequestPacket{MagicNumber:
                                                                                 magicNumber,
                                                                             Version:
                                                                                 version,
                                                                             CommandType:
                                                                                 unbox::<i32>(&box_(readByte(buffer.clone(),
                                                                                                             5_i32)
                                                                                                        as
                                                                                                        i32)),
                                                                             TotalPacketLength:
                                                                                 readInt32LittleEndian(buffer.clone(),
                                                                                                       6_i32),
                                                                             KeyLength:
                                                                                 readInt32LittleEndian(buffer.clone(),
                                                                                                       10_i32),
                                                                             ValueLength:
                                                                                 readInt32LittleEndian(buffer,
                                                                                                       14_i32),
                                                                             Key:
                                                                                 new_empty::<u8>(),
                                                                             Value:
                                                                                 None::<Array<u8>>,}))
                                          } else {
                                              None::<LrcPtr<RequestPacket>>
                                          }
                                      },
                                  |matchValue: LrcPtr<Exception>|
                                      None::<LrcPtr<RequestPacket>>)
                    }
                }
                pub fn TryParseRequestPacket(buffer: Array<u8>)
                 -> Option<LrcPtr<RequestPacket>> {
                    let matchValue: Option<LrcPtr<RequestPacket>> =
                        Ocis::Client::SDK::Protocol::TryParseRequestHeader(buffer.clone());
                    match &matchValue {
                        None => None::<LrcPtr<RequestPacket>>,
                        Some(matchValue_0_0) => {
                            let header: LrcPtr<RequestPacket> =
                                matchValue_0_0.clone();
                            if (get_Count(buffer.clone())) >=
                                   (header.TotalPacketLength) {
                                try_catch(||
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
                                                                                 getSubArray(buffer.clone(),
                                                                                             18_i32,
                                                                                             header.KeyLength),
                                                                             Value:
                                                                                 if (header.ValueLength)
                                                                                        >
                                                                                        0_i32
                                                                                    {
                                                                                     Some(getSubArray(buffer.clone(),
                                                                                                      18_i32
                                                                                                          +
                                                                                                          (header.KeyLength),
                                                                                                      header.ValueLength))
                                                                                 } else {
                                                                                     None::<Array<u8>>
                                                                                 },})),
                                          |matchValue_1: LrcPtr<Exception>|
                                              None::<LrcPtr<RequestPacket>>)
                            } else { None::<LrcPtr<RequestPacket>> }
                        }
                    }
                }
                pub fn SerializeRequest(packet: LrcPtr<RequestPacket>)
                 -> Array<u8> {
                    let keyLen: i32 = get_Count(packet.Key.clone());
                    let buffer: Array<u8> =
                        new_init(&0_u8,
                                 (18_i32 + (keyLen)) +
                                     (defaultValue(0_i32,
                                                   map(Func1::new(move
                                                                      |v:
                                                                           Array<u8>|
                                                                      get_Count(v)),
                                                       packet.Value.clone()))));
                    let offset: MutCell<i32> = MutCell::new(0_i32);
                    writeUInt32LittleEndian(packet.MagicNumber,
                                            buffer.clone(),
                                            offset.get().clone());
                    offset.set((offset.get().clone()) + 4_i32);
                    writeByte(packet.Version, buffer.clone(),
                              offset.get().clone());
                    offset.set((offset.get().clone()) + 1_i32);
                    writeByte(packet.CommandType as u8, buffer.clone(),
                              offset.get().clone());
                    offset.set((offset.get().clone()) + 1_i32);
                    writeInt32LittleEndian(packet.TotalPacketLength,
                                           buffer.clone(),
                                           offset.get().clone());
                    offset.set((offset.get().clone()) + 4_i32);
                    writeInt32LittleEndian(packet.KeyLength, buffer.clone(),
                                           offset.get().clone());
                    offset.set((offset.get().clone()) + 4_i32);
                    writeInt32LittleEndian(packet.ValueLength, buffer.clone(),
                                           offset.get().clone());
                    offset.set((offset.get().clone()) + 4_i32);
                    if (keyLen) > 0_i32 {
                        copyTo(packet.Key.clone(), 0_i32, buffer.clone(),
                               offset.get().clone(), keyLen);
                        offset.set((offset.get().clone()) + (keyLen))
                    }
                    {
                        let matchValue: Option<Array<u8>> =
                            packet.Value.clone();
                        match &matchValue {
                            None => (),
                            Some(matchValue_0_0) => {
                                let value_1: Array<u8> =
                                    matchValue_0_0.clone();
                                copyTo(value_1.clone(), 0_i32, buffer.clone(),
                                       offset.get().clone(),
                                       get_Count(value_1))
                            }
                        }
                    }
                    buffer.clone()
                }
                pub fn CreateSuccessResponse(value: Option<Array<u8>>)
                 -> LrcPtr<ResponsePacket> {
                    let patternInput: LrcPtr<(i32, i32)> =
                        match &value {
                            None => LrcPtr::new((0_i32, 18_i32)),
                            Some(value_0_0) => {
                                let v: Array<u8> = value_0_0.clone();
                                LrcPtr::new((get_Count(v.clone()),
                                             18_i32 + (get_Count(v))))
                            }
                        };
                    LrcPtr::new(ResponsePacket{MagicNumber: 1397310287_u32,
                                               Version: 1_u8,
                                               StatusCode: 0_u8,
                                               TotalPacketLength:
                                                   patternInput.1.clone(),
                                               ValueLength:
                                                   patternInput.0.clone(),
                                               ErrorMessageLength: 0_i32,
                                               Value: value.clone(),
                                               ErrorMessage: None::<string>,})
                }
                pub fn CreateNotFoundResponse() -> LrcPtr<ResponsePacket> {
                    LrcPtr::new(ResponsePacket{MagicNumber: 1397310287_u32,
                                               Version: 1_u8,
                                               StatusCode: 1_u8,
                                               TotalPacketLength: 18_i32,
                                               ValueLength: 0_i32,
                                               ErrorMessageLength: 0_i32,
                                               Value: None::<Array<u8>>,
                                               ErrorMessage: None::<string>,})
                }
                pub fn CreateErrorResponse(errorMessage: string)
                 -> LrcPtr<ResponsePacket> {
                    let msgBytes: Array<u8> =
                        get_UTF8().getBytes(errorMessage.clone());
                    LrcPtr::new(ResponsePacket{MagicNumber: 1397310287_u32,
                                               Version: 1_u8,
                                               StatusCode: 2_u8,
                                               TotalPacketLength:
                                                   18_i32 +
                                                       (get_Count(msgBytes.clone())),
                                               ValueLength: 0_i32,
                                               ErrorMessageLength:
                                                   get_Count(msgBytes),
                                               Value: None::<Array<u8>>,
                                               ErrorMessage:
                                                   Some(errorMessage),})
                }
                pub fn IsValidPacketSize(totalLength: i32) -> bool {
                    if (totalLength) >= 18_i32 {
                        (totalLength) <= ((10_i32 * 1024_i32) * 1024_i32)
                    } else { false }
                }
                pub fn SerializeResponse(packet: LrcPtr<ResponsePacket>)
                 -> Array<u8> {
                    let buffer: Array<u8> =
                        new_init(&0_u8,
                                 (18_i32 +
                                      (defaultValue(0_i32,
                                                    map(Func1::new(move
                                                                       |v:
                                                                            Array<u8>|
                                                                       get_Count(v)),
                                                        packet.Value.clone()))))
                                     +
                                     (defaultValue(0_i32,
                                                   map(Func1::new(move
                                                                      |m:
                                                                           string|
                                                                      get_Count(get_UTF8().getBytes(m))),
                                                       packet.ErrorMessage.clone()))));
                    let offset: MutCell<i32> = MutCell::new(0_i32);
                    writeUInt32LittleEndian(packet.MagicNumber,
                                            buffer.clone(),
                                            offset.get().clone());
                    offset.set((offset.get().clone()) + 4_i32);
                    writeByte(packet.Version, buffer.clone(),
                              offset.get().clone());
                    offset.set((offset.get().clone()) + 1_i32);
                    writeByte(packet.StatusCode as u8, buffer.clone(),
                              offset.get().clone());
                    offset.set((offset.get().clone()) + 1_i32);
                    writeInt32LittleEndian(packet.TotalPacketLength,
                                           buffer.clone(),
                                           offset.get().clone());
                    offset.set((offset.get().clone()) + 4_i32);
                    writeInt32LittleEndian(packet.ValueLength, buffer.clone(),
                                           offset.get().clone());
                    offset.set((offset.get().clone()) + 4_i32);
                    writeInt32LittleEndian(packet.ErrorMessageLength,
                                           buffer.clone(),
                                           offset.get().clone());
                    offset.set((offset.get().clone()) + 4_i32);
                    {
                        let matchValue: Option<Array<u8>> =
                            packet.Value.clone();
                        match &matchValue {
                            None => (),
                            Some(matchValue_0_0) => {
                                let value_2: Array<u8> =
                                    matchValue_0_0.clone();
                                copyTo(value_2.clone(), 0_i32, buffer.clone(),
                                       offset.get().clone(),
                                       get_Count(value_2.clone()));
                                offset.set((offset.get().clone()) +
                                               (get_Count(value_2)))
                            }
                        }
                    }
                    {
                        let matchValue_1: Option<string> =
                            packet.ErrorMessage.clone();
                        match &matchValue_1 {
                            None => (),
                            Some(matchValue_1_0_0) => {
                                let msg: string = matchValue_1_0_0.clone();
                                let msgBytes: Array<u8> =
                                    get_UTF8().getBytes(msg);
                                copyTo(msgBytes.clone(), 0_i32,
                                       buffer.clone(), offset.get().clone(),
                                       get_Count(msgBytes))
                            }
                        }
                    }
                    buffer.clone()
                }
                #[derive(Clone, Hash, PartialEq, PartialOrd,)]
                pub enum ParseResult_1<T: Clone + 'static> {
                    ParseSuccess(T),
                    ParseError(string),
                    InsufficientData,
                }
                impl <T: Clone + 'static> core::fmt::Debug for
                 ParseResult_1<T> {
                    fn fmt(&self, f: &mut core::fmt::Formatter)
                     -> core::fmt::Result {
                        write!(f, "{}", core::any::type_name::<Self>())
                    }
                }
                impl <T: Clone + 'static> core::fmt::Display for
                 ParseResult_1<T> {
                    fn fmt(&self, f: &mut core::fmt::Formatter)
                     -> core::fmt::Result {
                        write!(f, "{}", core::any::type_name::<Self>())
                    }
                }
                pub fn DeserializeResponse(buffer: Array<u8>)
                 ->
                     LrcPtr<Ocis::Client::SDK::Protocol::ParseResult_1<LrcPtr<ResponsePacket>>> {
                    try_catch(||
                                  if (get_Count(buffer.clone())) < 18_i32 {
                                      LrcPtr::new(Ocis::Client::SDK::Protocol::ParseResult_1::InsufficientData::<LrcPtr<ResponsePacket>>)
                                  } else {
                                      let offset: MutCell<i32> =
                                          MutCell::new(0_i32);
                                      let magicNumber: u32 =
                                          readUInt32LittleEndian(buffer.clone(),
                                                                 offset.get().clone());
                                      offset.set((offset.get().clone()) +
                                                     4_i32);
                                      {
                                          let version: u8 =
                                              readByte(buffer.clone(),
                                                       offset.get().clone());
                                          offset.set((offset.get().clone()) +
                                                         1_i32);
                                          {
                                              let statusCode: u8 =
                                                  readByte(buffer.clone(),
                                                           offset.get().clone())
                                                      as u8;
                                              offset.set((offset.get().clone())
                                                             + 1_i32);
                                              {
                                                  let totalPacketLength: i32 =
                                                      readInt32LittleEndian(buffer.clone(),
                                                                            offset.get().clone());
                                                  offset.set((offset.get().clone())
                                                                 + 4_i32);
                                                  {
                                                      let valueLength: i32 =
                                                          readInt32LittleEndian(buffer.clone(),
                                                                                offset.get().clone());
                                                      offset.set((offset.get().clone())
                                                                     + 4_i32);
                                                      {
                                                          let errorMessageLength:
                                                                  i32 =
                                                              readInt32LittleEndian(buffer.clone(),
                                                                                    offset.get().clone());
                                                          offset.set((offset.get().clone())
                                                                         +
                                                                         4_i32);
                                                          if (get_Count(buffer.clone()))
                                                                 <
                                                                 (totalPacketLength)
                                                             {
                                                              LrcPtr::new(Ocis::Client::SDK::Protocol::ParseResult_1::InsufficientData::<LrcPtr<ResponsePacket>>)
                                                          } else {
                                                              if !if (magicNumber)
                                                                         ==
                                                                         1397310287_u32
                                                                     {
                                                                      (version)
                                                                          ==
                                                                          1_u8
                                                                  } else {
                                                                      false
                                                                  } {
                                                                  LrcPtr::new(Ocis::Client::SDK::Protocol::ParseResult_1::ParseError::<LrcPtr<ResponsePacket>>(string("Invalid header")))
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
                                                                      LrcPtr::new(Ocis::Client::SDK::Protocol::ParseResult_1::ParseError::<LrcPtr<ResponsePacket>>(string("Invalid length field")))
                                                                  } else {
                                                                      if (totalPacketLength)
                                                                             !=
                                                                             ((18_i32
                                                                                   +
                                                                                   (valueLength))
                                                                                  +
                                                                                  (errorMessageLength))
                                                                         {
                                                                          LrcPtr::new(Ocis::Client::SDK::Protocol::ParseResult_1::ParseError::<LrcPtr<ResponsePacket>>(string("Packet length mismatch")))
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
                                                                          LrcPtr::new(Ocis::Client::SDK::Protocol::ParseResult_1::ParseSuccess::<LrcPtr<ResponsePacket>>(LrcPtr::new(ResponsePacket{MagicNumber:
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
                                  LrcPtr::new(Ocis::Client::SDK::Protocol::ParseResult_1::ParseError::<LrcPtr<ResponsePacket>>({
                                                                                                                                   let arg:
                                                                                                                                           string =
                                                                                                                                       ex.get_Message();
                                                                                                                                   sprintf!("Error parsing response: {}",
                                                                                                                                            arg)
                                                                                                                               })))
                }
            }
        }
    }
}
