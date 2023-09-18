# DKaf

Use the [Confluent.Kafka](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html) .NET library from within Dyalog APL.

Note: this is a proof of concept only.

## Pre-requisites

You need to have Dyalog and dotnet 6.0+ installed. 

We can't quite use the Confluent.Kafka library directly, due to the lack of generics support in the Dyalog .NET bridge. To work around this, we first need to build the thin C# shim:

1. Clone this repository, assuming `/home/you/DKaf`
2. `cd /home/you/DKaf/DyKa`
3. `dotnet build`
4. `dotnet publish -o /home/you/mybuild`

Confluent.Kafka is a wrapper around the native [librdkafka](https://github.com/confluentinc/librdkafka) library. This means that you'll need to copy the native dependencies from the `/home/you/mybuild/runtimes/{YOUR-ARCH}/native/` to `/home/you/mybuild` in order for the Dyalog .NET bridge to see them. For example, as I am on an Apple Silicon mac, I would need to type

```
cp /home/you/mybuild/runtimes/osx-arm64/native/* /home/you/mybuild/
```

## C# shim: DyKa

The C# bits do basically nothing: their only purpose is to hide the generics that are hard to use with dotnet from within Dyalog. The APL classes use the underlying `StringProducer` and `StringConsumer` c# classes, which expects messages to be of the type `<string, string>`. There are two other classes included, `Producer.cs` and `Consumer.cs` which use messages of the type `<string, byte[]>`, which is a bit more flexible, but then you have to do your own serialisation and deserialisation.

You can use the C# shim classes directly. 

## The DKaf APL wrapper

The APL classes included serve mainly as an illustration for how to use the C# DyKa library. In order to use them, first modify the `⎕USING` statements in `DKaf/DKaf/{Producer|Consumer}.aplc`. 

Currently they look like so:
```
⎕USING←'Confluent.Kafka,/Users/stefan/work/mybuild/Confluent.Kafka.dll'
⎕USING,←⊂',/Users/stefan/work/mybuild/DyKa.dll'
```

They need to reference your dotnet build directory `/home/you/mybuild`. Yes, this is ugly, and, yes, Dyalog will make this more palatable in a future release of the `cider/tatin` tool chain.

To use, `]link` it:

```
      ]link.create # /home/you/DKaf/DKaf
Linked: # ←→ /home/you/DKaf/DKaf
```

## Testing

Start Kafka, e.g. by using the included `docker-compose.yml`:

```
% docker compose up -d
[+] Running 3/3
 ✔ Network kafka_default  Created                                                  0.0s 
 ✔ Container zookeeper    Started                                                  0.2s 
 ✔ Container broker       Started                                                  0.3s
 ```

Assuming you have the kafka cli tools available, first make a topic:

```
% kafka-topics --create    \
    --topic bikes          \
    --partitions 1         \
    --replication-factor 1 \
    --bootstrap-server localhost:9092
```

Grab some test data. Here's one [suggested by Confluent](https://github.com/confluentinc/demo-scene/tree/master/confluent-xml-demo), involving live data about hire bikes in London. It uses the [kcat utility](https://github.com/edenhill/kcat), and the [xq](https://github.com/sibprogrammer/xq) tool for converting xml to json which you may have to install first (`pip install xq`):
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
          c ← ⎕NEW Consumer(0 'localhost:9092' 'group' 'bikes')
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
      
To produce and consume arbitrary APL arrays, use the included `serdes` function:

```
% kafka-topics --create    \
    --topic arrays         \
    --partitions 1         \
    --replication-factor 1 \
    --bootstrap-server localhost:9092
```

```apl
      producer ← ⎕NEW Producer(1 'localhost:9092')     ⍝ Make a byte producer
      data ← (?¨5⍴⍨?4)⍴1 2 (3 4 5) 'hello' (2 'world') ⍝ Make a randomishly shaped array
      data
┌→────────┐
↓         │
│ 1       │
│         │
│         │
│ 2       │
│         │
│ ┌→────┐ │
│ │3 4 5│ │
│ └~────┘ │
│ ┌→────┐ │
│ │hello│ │
│ └─────┘ │
└∊────────┘

      producer.Produce 'arrays' 'key' (serdes data)    ⍝ Encode the data array

      consumer ← ⎕NEW Consumer(1 'localhost:9092' 'group2' 'arrays')
      message ← 1 serdes consumer.Consume 3
      message
┌→────────┐
↓         │
│ 1       │
│         │
│         │
│ 2       │
│         │
│ ┌→────┐ │
│ │3 4 5│ │
│ └~────┘ │
│ ┌→────┐ │
│ │hello│ │
│ └─────┘ │
└∊────────┘
```

## Pipe a mixed array from Dyalog into sqlite3 via Kafka

In practice, you want to do this differently (Connect).

You need [jq](https://jqlang.github.io/jq/), [sqlite3](https://www.sqlite.org/index.html) and [kcat](https://github.com/edenhill/kcat), and a file containing a bunch of names (100 of them).

1. Create a sqlite3 database:
    ```
    sqlite3 employees.db "CREATE TABLE IF NOT EXISTS employees (id INTEGER, name TEXT, dept TEXT);"
    ```
2. Make a topic:
    ```
    kafka-topics --create --topic users --partitions 1 --replication-factor 1 --bootstrap-server localhost:9092
    ````
3. Generate some data:
    ```apl
    names ← ⊃⎕NGET 'names.txt'1 ⍝ Text file with one name per line
    depts ← (⊂⍤?⍨∘≢⌷⊢)100⍴'Engineering' 'Accounts' 'Ops' 'Sales' 'Support' ⍝ Make up some department names
    users ← ⎕NS¨(≢names)⍴⊂⍬
    users.name ← names
    users.dept ← depts
    users.id ← ⍳≢names
4. Write the data onto the topic as `⎕JSON`:
    ```apl
    p ← ⎕NEW Producer(0'localhost:9092')
    _←{_←p.Produce 'users' (⍕⍵.id) (⎕JSON⍵)⋄⍬}¨users
    ```
5. Read data from topic, parse JSON and write to sqlite3:
    ```sh
    kcat -Ct users -b localhost:9092 -o beginning -e | \
    while IFS= read -r line; do \
        sql=$(echo "$line" | jq -r '"INSERT INTO employees (id, name, dept) VALUES (\(.id), \"\(.name)\", \"\(.dept)\");"'); \
        echo "$sql" | sqlite3 employees.db; \
    done
    ```