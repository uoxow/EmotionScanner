Netlify → https://emotionscanner.netlify.app/    
使用した技術・ツール・環境
	OS：Windows11  
	AI学習：Google Teachable Machine  
	使用言語：C＃，Python  
	開発エンジン：Unity 6 (Sentisパッケージを使用)  
	Web公開：Netlify  
	バージョン管理：GitHub/Git  
	学習対象：手や体の動きを使わない顔の筋肉と首の角度だけの4つの状態    
開発手法  
Webカメラの映像をUnity上の推論エンジン（Sentis）に入力し、リアルタイムで解析を行う。学習モデルは事前にGoogle ColabにてONNX形式に変換したものを使用した。推論結果として得られたクラスID（0〜3）に基づき、C#スクリプトがアニメーションの遷移を制御した。
