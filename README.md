# DKaf

Use the [Confluent.Kafka](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html) .NET library from within Dyalog APL.

Pre-requisites: you have Dyalog and dotnet installed. 

To use, the first thing to do is to build the thin C# shim:

1. Clone this repository, assuming `/home/you/DKaf`
2. `cd /home/you/DKaf/DyKa`
3. `dotnet build`
4. `dotnet publish -o /home/you/mybuild`

Now you need to copy the native dependencies from the `/home/you/mybuild/runtimes/{YOUR-ARCH}/native/` to `/home/you/mybuild`. For example, as I am on an Apple Silicon mac, I would need to type

```
cp /home/you/mybuild/runtimes/osx-arm64/native/* /home/you/mybuild/`
```

Modify the `⎕USING` statements in `DKaf/DKaf/Producer|Consumer.aplc`. Currently they look like so:
```
⎕USING←'Confluent.Kafka,/Users/stefan/work/mybuild/Confluent.Kafka.dll'
⎕USING,←⊂',/Users/stefan/work/mybuild/DyKa.dll'
```

They need to reference your dotnet build directory `/home/you/mybuild`. Yes, this is ugly, and, yes, Dyalog will make this more palatable. 

Start Kafka, e.g. by using the included `docker-compose.yml`:

```
% docker compose up -d
[+] Running 3/3
 ✔ Network kafka_default  Created                                                  0.0s 
 ✔ Container zookeeper    Started                                                  0.2s 
 ✔ Container broker       Started                                                  0.3s
 ```

Assuming you have the kafka cli tools, first make a topic:

```
% kafka-topics --create    \
    --topic bikes          \
    --partitions 1         \
    --replication-factor 1 \
    --bootstrap-server localhost:9092
```

Grab some test data. Here's one [suggested by Confluent](https://github.com/confluentinc/demo-scene/tree/master/confluent-xml-demo), involving live data about hire bikes in London. It uses the [kcat utility](https://github.com/edenhill/kcat), and the `xq` tool for converting xml to json which you may have to install first (`pip install xq`):
```
curl --show-error --silent https://tfl.gov.uk/tfl/syndication/feeds/cycle-hire/livecyclehireupdates.xml | xq -c '.stations.station[] + {lastUpdate: .stations."@lastUpdate"}' | kcat -Pt bikes -b localhost:9092
```

If that all went well, start your APL session.

1. Link the `home/you/DKaf/DKaf` directory:
    ```
          ]link.create # /home/you/DKaf/DKaf
    Linked: # ←→ /home/you/DKaf/DKaf
    ```
2. Create a `Consumer`, and read a few messages off the `bikes` topic:
    ```apl
          c ← ⎕NEW Consumer('localhost:9092' 'group' 'bikes')
          r←⍬
          _←{r,←⊂c.Consume 1⋄⍬}⍣10⊢⍬
          ↑70↑¨3↑r ⍝ Show the first 70 characters of a copuple of messages received
    {"id":"11","name":"Brunswick Square, Bloomsbury","terminalName":"00102
    {"id":"12","name":"Malet Street, Bloomsbury","terminalName":"000980","
    {"id":"13","name":"Scala Street, Fitzrovia","terminalName":"000970","l
          bike ← ⎕JSON⊃r
          bike.lat
    51.52395143
    ```
      
## DyKa

The C# bits do basically nothing: their only purpose is to hide the generics that are hard to use with dotnet from within Dyalog. The APL classes use the underlying `StringProducer` and `StringConsumer` c# classes, which expects messages to be of the type `<string, string>`. There are two other classes included, `Producer.cs` and `Consumer.cs` which use messages of the type `<string, byte[]>`, which is a bit more flexible, but then you have to do your own serialisation and deserialisation.