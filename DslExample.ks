



func Ingest() -> (string r, bool IsResultGeezy {
	r = "1234" // Inferred Allocation from Result
	if (r == "Geezy")
	{
	  IsResultGeezy = true; 
	}
	return r

}

dag EtlDemo {
	node Ingest { 
		func: Ingest,
		args: null,
	}
	node Transform {
		func: EtlDemo.Io.FuncFromFile("path"),
		args: IsResultGeezy,
	}
	Ingest | Transform | cmd3 | ... ; // use for non branching pipelines;
	
	Ingest -> Transform; // use for branching lines(edges)
}

func Main(string[] args){
	RuntimeConfig = {
		LogLevel: Dbug,
		RetryPolicy: BackoffExponential, // Or NoRetry, or BackoffLinear,
		RetryCount: 3,
		RetryDelay: 1000, // in milliseconds,

		TrueParalell: true, // if false, the dag will run in a single thread,
		Threads: 4,
	}
	EtlDemo.SetSchedule("0 0 * * *"); // uses same format as cron
	EtlDemo.RunNow();


}

