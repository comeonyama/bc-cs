using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using QBitNinja.Client;
using QBitNinja.Client.Models;

namespace SpendYourCoins
{
    class Program
    {
        static void Main()
        {

            #region IMPORT PRIVKEY
            var bitcoinPrivateKey = new BitcoinSecret("自分のウオレットの秘密鍵");
            var network = bitcoinPrivateKey.Network;
            #endregion

            // ビットコインアドレスの導出
            var address = bitcoinPrivateKey.GetAddress();

            Console.WriteLine(bitcoinPrivateKey);
            Console.WriteLine(network); // TestNet or MainNet 
            Console.WriteLine(address);
            Console.WriteLine();

            var client = new QBitNinjaClient(network);
            var transactionId = uint256.Parse("使いたいコインが含まれるトランザクションID");
            var transactionResponse = client.GetTransaction(transactionId).Result;

            Console.WriteLine(transactionResponse.TransactionId);
            Console.WriteLine(transactionResponse.Block.Confirmations);
            Console.WriteLine();

            // トランザクションインプット：送金元情報を組み立てる
            var receivedCoins = transactionResponse.ReceivedCoins;
            OutPoint outPointToSpend = null;
            foreach (var coin in receivedCoins)
            {
                if (coin.TxOut.ScriptPubKey == bitcoinPrivateKey.ScriptPubKey)
                {
                    outPointToSpend = coin.Outpoint;
                }
            }
            if (outPointToSpend == null)
                throw new Exception("TxOut doesn't contain our ScriptPubKey");
            Console.WriteLine("We want to spend {0}. outpoint:", outPointToSpend.N + 1);

            var transaction = new Transaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = outPointToSpend
            });

            var destinationAddress = new BitcoinPubKeyAddress("送金先のビットコインアドレス");

            // いくら相手に送るか？
            var destinationAmount = new Money((decimal)0.0065, MoneyUnit.BTC);
            // 送金手数料 ここでは0.0005BTCとしていますが、市場価格に応じて増減してください。
            var minerFee = new Money((decimal)0.0005, MoneyUnit.BTC);
            // 入力の総額（入力の全額としなければならない）
            var txInAmount = (Money)receivedCoins[(int)outPointToSpend.N].Amount;
            // 入力の総額から送金分を引いて自分に戻す分
            Money changeBackAmount = txInAmount - destinationAmount - minerFee;

            //トランザクションアウトプットの組み立て
            //送金先
            TxOut destinationTxOut = new TxOut()
            {
                Value = destinationAmount,
                ScriptPubKey = destinationAddress.ScriptPubKey
            };
            //自分に戻す分
            TxOut changeBackTxOut = new TxOut()
            {
                Value = changeBackAmount,
                ScriptPubKey = bitcoinPrivateKey.ScriptPubKey
            };

            transaction.Outputs.Add(destinationTxOut);

            var message = "メッセージとかあればここに";
            var bytes = Encoding.UTF8.GetBytes(message);
            transaction.Outputs.Add(new TxOut()
            {
                Value = Money.Zero,
                ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
            });

            transaction.Inputs[0].ScriptSig = address.ScriptPubKey;

            // It is also OK:
            transaction.Inputs[0].ScriptSig = bitcoinPrivateKey.ScriptPubKey;
            transaction.Sign(bitcoinPrivateKey, false);

            Console.WriteLine(transaction.ToString());

            BroadcastResponse broadcastResponse = client.Broadcast(transaction).Result;

            if (!broadcastResponse.Success)
            {
                Console.WriteLine(string.Format("ErrorCode: {0}", broadcastResponse.Error.ErrorCode));
                Console.WriteLine("Error message: " + broadcastResponse.Error.Reason);
            }
            else
            {
                Console.WriteLine("Success! You can check out the hash of the transaciton in any block explorer:");
                Console.WriteLine(transaction.GetHash());
            }

            Console.ReadLine();
        }
    }
