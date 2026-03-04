import { Union } from "./fable_modules/fable-library-ts.4.25.0/Types.js";
import { union_type, string_type, TypeInfo } from "./fable_modules/fable-library-ts.4.25.0/Reflection.js";
import { ParseResult$1_$union, DeserializeResponse } from "./Protocol.js";
import { uint8 } from "./fable_modules/fable-library-ts.4.25.0/Int32.js";
import { ResponsePacket } from "./Ocis.Server/ProtocolSpec.js";
import { value as value_2, Option, defaultArg } from "./fable_modules/fable-library-ts.4.25.0/Option.js";

export type ClientResult$1_$union<T> = 
    | ClientResult$1<T, 0>
    | ClientResult$1<T, 1>
    | ClientResult$1<T, 2>

export type ClientResult$1_$cases<T> = {
    0: ["Success", [T]],
    1: ["NotFound", []],
    2: ["Error", [string]]
}

export function ClientResult$1_Success<T>(Item: T) {
    return new ClientResult$1<T, 0>(0, [Item]);
}

export function ClientResult$1_NotFound<T>() {
    return new ClientResult$1<T, 1>(1, []);
}

export function ClientResult$1_Error<T>(Item: string) {
    return new ClientResult$1<T, 2>(2, [Item]);
}

export class ClientResult$1<T, Tag extends keyof ClientResult$1_$cases<T>> extends Union<Tag, ClientResult$1_$cases<T>[Tag][0]> {
    constructor(readonly tag: Tag, readonly fields: ClientResult$1_$cases<T>[Tag][1]) {
        super();
    }
    cases() {
        return ["Success", "NotFound", "Error"];
    }
}

export function ClientResult$1_$reflection(gen0: TypeInfo): TypeInfo {
    return union_type("Ocis.Client.SDK.Response.ClientResult`1", [gen0], ClientResult$1, () => [[["Item", gen0]], [], [["Item", string_type]]]);
}

export function parseResponse(bytes: uint8[]): ParseResult$1_$union<ResponsePacket> {
    return DeserializeResponse(bytes);
}

export function toClientResult(parseResult: ParseResult$1_$union<ResponsePacket>): ClientResult$1_$union<void> {
    switch (parseResult.tag) {
        case /* ParseError */ 1:
            return ClientResult$1_Error<void>(parseResult.fields[0]);
        case /* InsufficientData */ 2:
            return ClientResult$1_Error<void>("Insufficient data");
        default: {
            const response: ResponsePacket = parseResult.fields[0];
            const matchValue: uint8 = response.StatusCode;
            switch (matchValue) {
                case 0:
                    return ClientResult$1_Success<void>(undefined);
                case 1:
                    return ClientResult$1_NotFound<void>();
                case 2:
                    return ClientResult$1_Error<void>(defaultArg(response.ErrorMessage, "Unknown error"));
                default:
                    return ClientResult$1_Error<void>("Invalid status code");
            }
        }
    }
}

export function toClientResultValue(parseResult: ParseResult$1_$union<ResponsePacket>): ClientResult$1_$union<uint8[]> {
    switch (parseResult.tag) {
        case /* ParseError */ 1:
            return ClientResult$1_Error<uint8[]>(parseResult.fields[0]);
        case /* InsufficientData */ 2:
            return ClientResult$1_Error<uint8[]>("Insufficient data");
        default: {
            const response: ResponsePacket = parseResult.fields[0];
            const matchValue: uint8 = response.StatusCode;
            switch (matchValue) {
                case 0: {
                    const matchValue_1: Option<uint8[]> = response.Value;
                    if (matchValue_1 == null) {
                        return ClientResult$1_Error<uint8[]>("Success response missing value");
                    }
                    else {
                        return ClientResult$1_Success<uint8[]>(value_2(matchValue_1));
                    }
                }
                case 1:
                    return ClientResult$1_NotFound<uint8[]>();
                case 2:
                    return ClientResult$1_Error<uint8[]>(defaultArg(response.ErrorMessage, "Unknown error"));
                default:
                    return ClientResult$1_Error<uint8[]>("Invalid status code");
            }
        }
    }
}

