const net = require('net');
const { CreateRequest, SerializeRequest, DeserializeResponse } = require('./Library');
const protocolSpec = require("../ProtocolSpec");

const client = new net.Socket();

client.connect(7379, '127.0.0.1', () => {
    console.log('connected to server');

    // write key value pair
    client.write(SerializeRequest(CreateRequest(1, Buffer.from('test'), Buffer.from('test'))));

    // get value by key
    // client.write(SerializeRequest(CreateRequest(2, Buffer.from('test'))));
});

client.on('data', (data) => {
    const responseArray = new Uint8Array(data);
    const response = DeserializeResponse(responseArray);
    response.name === 'ParseSuccess' && console.log('response:', response.fields[0]);
    response.name === 'ParseError' && console.error('error:', response.fields[0]);
    client.destroy();
});

client.on('close', () => {
    console.log('connection closed');
});

client.on('error', (err) => {
    console.error('error:', err);
});
