from library import *
import socket

client = socket.socket()

client.connect(('127.0.0.1', 7379))

# write key value pair
client.send(SerializeRequest(CreateRequest(1, bytearray('test'.encode()), bytearray('test'.encode()))))

# get value by key
# client.send(SerializeRequest(CreateRequest(2, bytearray('test'.encode()))))

data = client.recv(1024)
response = DeserializeResponse(bytearray(data))

if response.name == 'ParseSuccess':
    print(response.fields[0])

elif response.name == 'ParseError':
    print(response.fields[0])

client.close()