import { Record } from "./ocis-protocol-ts/fable_modules/fable-library-ts.4.25.0/Types.js";
import { int32, uint8, uint32 } from "./ocis-protocol-ts/fable_modules/fable-library-ts.4.25.0/Int32.js";
import { Option } from "./ocis-protocol-ts/fable_modules/fable-library-ts.4.25.0/Option.js";
import { IComparable, IEquatable } from "./ocis-protocol-ts/fable_modules/fable-library-ts.4.25.0/Util.js";
import { string_type, record_type, option_type, array_type, enum_type, int32_type, uint8_type, uint32_type, TypeInfo } from "./ocis-protocol-ts/fable_modules/fable-library-ts.4.25.0/Reflection.js";

export class RequestPacket extends Record implements IEquatable<RequestPacket>, IComparable<RequestPacket> {
    readonly MagicNumber: uint32;
    readonly Version: uint8;
    readonly CommandType: int32;
    readonly TotalPacketLength: int32;
    readonly KeyLength: int32;
    readonly ValueLength: int32;
    readonly Key: uint8[];
    readonly Value: Option<uint8[]>;
    constructor(MagicNumber: uint32, Version: uint8, CommandType: int32, TotalPacketLength: int32, KeyLength: int32, ValueLength: int32, Key: uint8[], Value: Option<uint8[]>) {
        super();
        this.MagicNumber = MagicNumber;
        this.Version = Version;
        this.CommandType = (CommandType | 0);
        this.TotalPacketLength = (TotalPacketLength | 0);
        this.KeyLength = (KeyLength | 0);
        this.ValueLength = (ValueLength | 0);
        this.Key = Key;
        this.Value = Value;
    }
}

export function RequestPacket_$reflection(): TypeInfo {
    return record_type("Ocis.Server.ProtocolSpec.RequestPacket", [], RequestPacket, () => [["MagicNumber", uint32_type], ["Version", uint8_type], ["CommandType", enum_type("Ocis.Server.ProtocolSpec.CommandType", int32_type, [["Set", 1], ["Get", 2], ["Delete", 3]])], ["TotalPacketLength", int32_type], ["KeyLength", int32_type], ["ValueLength", int32_type], ["Key", array_type(uint8_type)], ["Value", option_type(array_type(uint8_type))]]);
}

export class ResponsePacket extends Record implements IEquatable<ResponsePacket>, IComparable<ResponsePacket> {
    readonly MagicNumber: uint32;
    readonly Version: uint8;
    readonly StatusCode: uint8;
    readonly TotalPacketLength: int32;
    readonly ValueLength: int32;
    readonly ErrorMessageLength: int32;
    readonly Value: Option<uint8[]>;
    readonly ErrorMessage: Option<string>;
    constructor(MagicNumber: uint32, Version: uint8, StatusCode: uint8, TotalPacketLength: int32, ValueLength: int32, ErrorMessageLength: int32, Value: Option<uint8[]>, ErrorMessage: Option<string>) {
        super();
        this.MagicNumber = MagicNumber;
        this.Version = Version;
        this.StatusCode = StatusCode;
        this.TotalPacketLength = (TotalPacketLength | 0);
        this.ValueLength = (ValueLength | 0);
        this.ErrorMessageLength = (ErrorMessageLength | 0);
        this.Value = Value;
        this.ErrorMessage = ErrorMessage;
    }
}

export function ResponsePacket_$reflection(): TypeInfo {
    return record_type("Ocis.Server.ProtocolSpec.ResponsePacket", [], ResponsePacket, () => [["MagicNumber", uint32_type], ["Version", uint8_type], ["StatusCode", enum_type("Ocis.Server.ProtocolSpec.StatusCode", uint8_type, [["Success", 0], ["NotFound", 1], ["Error", 2]])], ["TotalPacketLength", int32_type], ["ValueLength", int32_type], ["ErrorMessageLength", int32_type], ["Value", option_type(array_type(uint8_type))], ["ErrorMessage", option_type(string_type)]]);
}

